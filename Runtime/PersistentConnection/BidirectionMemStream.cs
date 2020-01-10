using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;
using System.IO;
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
using UnityEngine;
#if !NET_4_6 && !NET_STANDARD_2_0
using Unity.Collections.Concurrent;
#else
using System.Collections.Concurrent;
#endif
#else
using System.Collections.Concurrent;
#endif

namespace Capstones.Net
{
    public interface IBuffered
    {
        int BufferedSize { get; }
    }

    public struct BufferInfo
    {
        public BufferInfo(IPooledBuffer buffer, int cnt)
        {
            Buffer = buffer;
            Count = cnt;
            Raw = null;
            Serializer = null;
        }
        public BufferInfo(object raw, SendSerializer serializer)
        {
            Buffer = null;
            Count = 0;
            Raw = raw;
            Serializer = serializer;
        }

        public IPooledBuffer Buffer;
        public int Count;
        public object Raw;
        public SendSerializer Serializer;
    }

    public interface IPooledBuffer
    {
        byte[] Buffer { get; }
        void AddRef();
        void Release();
    }
    public class UnpooledBuffer : IPooledBuffer
    {
        public byte[] Buffer { get; set; }
        public void AddRef()
        {
        }
        public void Release()
        {
        }

        public UnpooledBuffer(byte[] raw)
        {
            Buffer = raw;
        }

        //public static implicit operator byte[](UnpooledBuffer thiz)
        //{
        //    return thiz.Buffer;
        //}
        //public static implicit operator UnpooledBuffer(byte[] raw)
        //{
        //    return new UnpooledBuffer(raw);
        //}
    }
    public static class BufferPool
    {
        private const int _LARGE_POOL_LEVEL_CNT = 10;
        private const int _LARGE_POOL_SLOT_CNT_PER_LEVEL = 4;
        private const int _BufferDefaultSize = CONST.MTU;

        private static ConcurrentQueueFixedSize<byte[]> _DefaultPool = new ConcurrentQueueFixedSize<byte[]>();
        private static int[] _LargePoolCounting = new int[_LARGE_POOL_LEVEL_CNT];
        private static byte[][] _LargePool = new byte[_LARGE_POOL_LEVEL_CNT * _LARGE_POOL_SLOT_CNT_PER_LEVEL][];

#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
        private static HashSet<byte[]> _DebugPool = new HashSet<byte[]>();
#endif

        private static void ReturnRawBufferToPool(byte[] buffer)
        {
            if (buffer != null)
            {
                var len = buffer.Length;
                if (len == _BufferDefaultSize)
                {
#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
                    lock (_DebugPool)
                    {
                        if (!_DebugPool.Add(buffer))
                        {
                            Debug.LogError("Returned Twice!!!");
                        }
                    }
#endif
                    _DefaultPool.Enqueue(buffer);
                }
                else if (len >= _BufferDefaultSize * 2)
                {
                    var level = len / _BufferDefaultSize - 2;
                    if (level < _LARGE_POOL_LEVEL_CNT)
                    {
                        var index = System.Threading.Interlocked.Increment(ref _LargePoolCounting[level]);
                        if (index > _LARGE_POOL_SLOT_CNT_PER_LEVEL)
                        {
                            System.Threading.Interlocked.Decrement(ref _LargePoolCounting[level]);
                        }
                        else
                        {
                            var eindex = level * _LARGE_POOL_SLOT_CNT_PER_LEVEL + index - 1;
#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
                            lock (_DebugPool)
                            {
                                if (!_DebugPool.Add(buffer))
                                {
                                    Debug.LogError("Returned Twice!!! (Large)");
                                }
                            }
#endif
                            SpinWait spin = new SpinWait();
                            while (System.Threading.Interlocked.CompareExchange(ref _LargePool[eindex], buffer, null) != null) spin.SpinOnce();
                        }
                    }
                }
            }
        }
        private static byte[] GetRawBufferFromPool()
        {
            return GetRawBufferFromPool(0);
        }
        private static byte[] GetRawBufferFromPool(int minsize)
        {
            if (minsize < _BufferDefaultSize)
            {
                minsize = _BufferDefaultSize;
            }
            if (minsize == _BufferDefaultSize)
            {
                byte[] old;
                if (_DefaultPool.TryDequeue(out old))
                {
#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
                    lock (_DebugPool)
                    {
                        _DebugPool.Remove(old);
                    }
#endif
                    return old;
                }
            }
            else
            {
                var level = (minsize - 1) / _BufferDefaultSize - 1;
                if (level < _LARGE_POOL_LEVEL_CNT)
                {
                    minsize = (level + 2) * _BufferDefaultSize;
                    var index = System.Threading.Interlocked.Decrement(ref _LargePoolCounting[level]);
                    if (index < 0)
                    {
                        System.Threading.Interlocked.Increment(ref _LargePoolCounting[level]);
                    }
                    else
                    {
                        var eindex = level * _LARGE_POOL_SLOT_CNT_PER_LEVEL + index;
                        SpinWait spin = new SpinWait();
                        while (true)
                        {
                            var old = _LargePool[eindex];
                            if (old != null && System.Threading.Interlocked.CompareExchange(ref _LargePool[eindex], null, old) == old)
                            {
#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
                                lock (_DebugPool)
                                {
                                    _DebugPool.Remove(old);
                                }
#endif
                                return old;
                            }
                            spin.SpinOnce();
                        }
                    }
                }
            }
            return new byte[minsize];
        }

        private static ConcurrentQueueFixedSize<PooledBuffer> _WrapperPool = new ConcurrentQueueFixedSize<PooledBuffer>();
        private static PooledBuffer GetWrapperFromPool()
        {
            PooledBuffer wrapper;
            if (!_WrapperPool.TryDequeue(out wrapper))
            {
                wrapper = new PooledBuffer();
            }
            Interlocked.Exchange(ref wrapper.RefCount, 1);
            wrapper.Buffer = null;
            return wrapper;
        }
        private static void ReturnWrapperToPool(PooledBuffer wrapper)
        {
            if (wrapper != null)
            {
                Interlocked.Exchange(ref wrapper.RefCount, 0);
                wrapper.Buffer = null;
                _WrapperPool.Enqueue(wrapper);
            }
        }
        private class PooledBuffer : IPooledBuffer
        {
            public int RefCount = 0;

            public byte[] Buffer { get; set; }

            public void AddRef()
            {
                var refcnt = Interlocked.Increment(ref RefCount);
#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
                if (refcnt <= 1)
                {
                    PlatDependant.LogError("Try AddRef a buffer, when it is already dead.");
                }
#endif
            }

            public void Release()
            {
                var refcnt = Interlocked.Decrement(ref RefCount);
                if (refcnt == 0)
                {
                    ReturnRawBufferToPool(Buffer);
                    ReturnWrapperToPool(this);
                }
#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
                else if (refcnt < 0)
                {
                    PlatDependant.LogError("Try release a buffer, when it is already dead.");
                }
#endif
            }
        }

        public static IPooledBuffer GetBufferFromPool()
        {
            var wrapper = GetWrapperFromPool();
            wrapper.Buffer = GetRawBufferFromPool();
            return wrapper;
        }
        public static IPooledBuffer GetBufferFromPool(int minsize)
        {
            var wrapper = GetWrapperFromPool();
            wrapper.Buffer = GetRawBufferFromPool(minsize);
            return wrapper;
        }
    }

    public class BidirectionMemStream : Stream, IBuffered
    {
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return -1; } }
        public override long Position { get { return -1; } set { } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { return -1; }
        public override void SetLength(long value) { }

        private ConcurrentQueueGrowOnly<BufferInfo> _Buffer = new ConcurrentQueueGrowOnly<BufferInfo>();
        private int _BufferOffset = 0;
        private AutoResetEvent _DataReady = new AutoResetEvent(false);
        private volatile bool _Closed = false;

        private int _Timeout = -1;
        public int Timeout { get { return _Timeout; } set { _Timeout = value; } }

        private int _BufferedSize = 0;
        public int BufferedSize { get { return _BufferedSize; } }

        /// <remarks>Should NOT be called from multi-thread.
        /// Please only read from one single thread.
        /// Reading and Writing can be in different thread.</remarks>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_Closed)
            {
                return 0;
            }
            while (true)
            {
                if (!_DataReady.WaitOne(_Timeout))
                {
                    return 0;
                }
                if (_Closed)
                {
                    _DataReady.Set();
                    return 0;
                }
                BufferInfo binfo;
                int rcnt = 0;
                while (rcnt < count && _Buffer.TryPeek(out binfo))
                {
                    bool binfoHaveData = true;
                    while (rcnt < count && binfoHaveData)
                    {
                        var prcnt = binfo.Count - _BufferOffset;
                        bool readlessthanbuffer = rcnt + prcnt > count;
                        if (readlessthanbuffer)
                        {
                            prcnt = count - rcnt;
                        }
                        Buffer.BlockCopy(binfo.Buffer.Buffer, _BufferOffset, buffer, offset + rcnt, prcnt);
                        if (readlessthanbuffer)
                        {
                            _BufferOffset += prcnt;
                        }
                        else
                        {
                            _Buffer.TryDequeue(out binfo);
                            binfo.Buffer.Release();
                            binfoHaveData = false;
                            _BufferOffset = 0;
                        }
                        rcnt += prcnt;
                    }
                }
                int bsize = _BufferedSize;
                int nbsize;
                SpinWait spin = new SpinWait();
                while (bsize != (nbsize = Interlocked.CompareExchange(ref _BufferedSize, bsize - rcnt, bsize)))
                {
                    spin.SpinOnce();
                    bsize = nbsize;
                }
                if (bsize > 0)
                {
                    _DataReady.Set();
                }
                if (rcnt > 0)
                {
                    return rcnt;
                }
            }
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            int cntwrote = 0;
            while (cntwrote < count)
            {
                var pbuffer = BufferPool.GetBufferFromPool();
                var sbuffer = pbuffer.Buffer;
                int scnt = count - cntwrote;
                if (sbuffer.Length < scnt)
                {
                    scnt = sbuffer.Length;
                }
                Buffer.BlockCopy(buffer, offset + cntwrote, sbuffer, 0, scnt);

                _Buffer.Enqueue(new BufferInfo(pbuffer, scnt));

                cntwrote += scnt;
            }
            int bsize = _BufferedSize;
            int nbsize;
            SpinWait spin = new SpinWait();
            while (bsize != (nbsize = Interlocked.CompareExchange(ref _BufferedSize, bsize + count, bsize)))
            {
                spin.SpinOnce();
                bsize = nbsize;
            }
            _DataReady.Set();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _Closed = true;
            _DataReady.Set();
        }
    }
}

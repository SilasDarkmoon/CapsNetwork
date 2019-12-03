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
        public BufferInfo(byte[] buffer, int cnt)
        {
            Buffer = buffer;
            Count = cnt;
        }

        public byte[] Buffer;
        public int Count;
    }

    public static class BufferPool
    {
        private const int _LARGE_POOL_LEVEL_CNT = 10;
        private const int _LARGE_POOL_SLOT_CNT_PER_LEVEL = 4;
        private const int _BufferDefaultSize = CONST.MTU;

        private static ConcurrentQueue<byte[]> _DefaultPool = new ConcurrentQueue<byte[]>();
        private static int[] _LargePoolCounting = new int[_LARGE_POOL_LEVEL_CNT];
        private static byte[][] _LargePool = new byte[_LARGE_POOL_LEVEL_CNT * _LARGE_POOL_SLOT_CNT_PER_LEVEL][];

#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
        private static HashSet<byte[]> _DebugPool = new HashSet<byte[]>();
#endif

        public static void ReturnBufferToPool(byte[] buffer)
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
                            while (System.Threading.Interlocked.CompareExchange(ref _LargePool[eindex], buffer, null) != null) ;
                        }
                    }
                }
            }
        }
        public static byte[] GetBufferFromPool()
        {
            return GetBufferFromPool(0);
        }
        public static byte[] GetBufferFromPool(int minsize)
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
                        }
                    }
                }
            }
            return new byte[minsize];
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

        private ConcurrentQueue<BufferInfo> _Buffer = new ConcurrentQueue<BufferInfo>();
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
                        Buffer.BlockCopy(binfo.Buffer, _BufferOffset, buffer, offset + rcnt, prcnt);
                        if (readlessthanbuffer)
                        {
                            _BufferOffset += prcnt;
                        }
                        else
                        {
                            _Buffer.TryDequeue(out binfo);
                            BufferPool.ReturnBufferToPool(binfo.Buffer);
                            binfoHaveData = false;
                            _BufferOffset = 0;
                        }
                        rcnt += prcnt;
                    }
                }
                int bsize = _BufferedSize;
                int nbsize;
                while (bsize != (nbsize = Interlocked.CompareExchange(ref _BufferedSize, bsize - rcnt, bsize)))
                {
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
                var sbuffer = BufferPool.GetBufferFromPool();
                int scnt = count - cntwrote;
                if (sbuffer.Length < scnt)
                {
                    scnt = sbuffer.Length;
                }
                Buffer.BlockCopy(buffer, offset + cntwrote, sbuffer, 0, scnt);

                _Buffer.Enqueue(new BufferInfo(sbuffer, scnt));

                cntwrote += scnt;
            }
            int bsize = _BufferedSize;
            int nbsize;
            while (bsize != (nbsize = Interlocked.CompareExchange(ref _BufferedSize, bsize + count, bsize)))
            {
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

//#define DISABLE_PERSIST_CONNECT_BUFFER_POOL
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

    public struct MessageInfo
    {
        public MessageInfo(IPooledBuffer buffer, int cnt)
        {
            buffer.AddRef();
            Buffers = new ValueList<PooledBufferSpan>()
            {
                new PooledBufferSpan()
                {
                    WholeBuffer = buffer,
                    Length = cnt,
                }
            };
            Raw = null;
            Serializer = null;
        }
        public MessageInfo(object raw, SendSerializer serializer)
        {
            Buffers = new ValueList<PooledBufferSpan>();
            Raw = raw;
            Serializer = serializer;
        }
        public MessageInfo(ValueList<PooledBufferSpan> buffers)
        {
            Buffers = buffers;
            Raw = null;
            Serializer = null;
        }

        public ValueList<PooledBufferSpan> Buffers;
        public object Raw;
        public SendSerializer Serializer;
    }

    public interface IPooledBuffer
    {
        byte[] Buffer { get; }
        int Length { get; }
        void AddRef();
        void Release();
    }
    public class UnpooledBuffer : IPooledBuffer
    {
        public byte[] Buffer { get; set; }
        public int Length { get { return Buffer.Length; } }
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
    public struct PooledBufferSpan : IPooledBuffer
    {
        public IPooledBuffer WholeBuffer;

        public byte[] Buffer { get { return WholeBuffer.Buffer; } }

        private int? _Length;
        public int Length
        {
            get { return _Length ?? WholeBuffer.Length; }
            set { _Length = value; }
        }

        public void AddRef()
        {
            WholeBuffer.AddRef();
        }

        public void Release()
        {
            WholeBuffer.Release();
        }
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
#if DISABLE_PERSIST_CONNECT_BUFFER_POOL
            return;
#endif
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
                            PlatDependant.LogError("Returned Twice!!!");
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
                                    PlatDependant.LogError("Returned Twice!!! (Large)");
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
#if DISABLE_PERSIST_CONNECT_BUFFER_POOL
            return new byte[CONST.MTU];
#endif
            return GetRawBufferFromPool(0);
        }
        private static byte[] GetRawBufferFromPool(int minsize)
        {
#if DISABLE_PERSIST_CONNECT_BUFFER_POOL
            return new byte[minsize];
#endif
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
#if DISABLE_PERSIST_CONNECT_BUFFER_POOL
            return new PooledBuffer();
#endif
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
#if DISABLE_PERSIST_CONNECT_BUFFER_POOL
            return;
#endif
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
            public int Length { get { return Buffer.Length; } }

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

#if DEBUG_PERSIST_CONNECT_BUFFER_POOL
            ~PooledBuffer()
            {
                PlatDependant.LogError("Finalizing a PooledBuffer. May missed release.");
            }
#endif
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

        public static ValueList<PooledBufferSpan> GetPooledBufferList(byte[] buffer, int offset, int count)
        {
            var rv = new ValueList<PooledBufferSpan>();
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
                rv.Add(new PooledBufferSpan() { WholeBuffer = pbuffer, Length = scnt });
                cntwrote += scnt;
            }
            return rv;
        }
    }

    public struct BufferInfo
    {
        public BufferInfo(IPooledBuffer buffer, int cnt)
        {
            Buffer = buffer;
            Count = cnt;
        }

        public IPooledBuffer Buffer;
        public int Count;
    }

    public class BidirectionMemStream : Stream, IBuffered
    {
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return _BufferedSize; } }
        public override long Position { get { return 0; } set { Seek(value, SeekOrigin.Current); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin)
        {
#if MULTITHREAD_SLOW_AND_SAFE
            lock (_Buffer)
            { 
#endif
            if (offset > (long)int.MaxValue)
            {
                offset = int.MaxValue;
            }
            else if (offset < (long)int.MinValue)
            {
                offset = int.MinValue;
            }
            if (origin == SeekOrigin.End)
            {
                var left = (int)-offset;
                if (left >= 0)
                {
                    int bsize = Volatile.Read(ref _BufferedSize);
                    Read(null, 0, bsize, left);
                }
                else
                {
                    int bsize = Volatile.Read(ref _BufferedSize);
                    Read(null, 0, bsize, 0);
                    Read(null, 0, -left);
                }
            }
            else
            {
                if (offset > 0)
                {
                    Read(null, 0, (int)offset);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            return 0;
#if MULTITHREAD_SLOW_AND_SAFE
            }
#endif
       }
        public override void SetLength(long value) { throw new NotSupportedException(); }

#if MULTITHREAD_SLOW_AND_SAFE || !UNITY_ENGINE && !UNITY_5_3_OR_NEWER
        private ConcurrentQueue<BufferInfo> _Buffer = new ConcurrentQueue<BufferInfo>();
#else
        private ConcurrentQueueGrowOnly<BufferInfo> _Buffer = new ConcurrentQueueGrowOnly<BufferInfo>();
#endif
        private volatile int _ReadingHeadConsumed = 0;
        private AutoResetEvent _DataReady = new AutoResetEvent(false);
        private volatile bool _Closed = false;

        private int _Timeout = -1;
        public int Timeout { get { return _Timeout; } set { _Timeout = value; } }

        private int _BufferedSize = 0;
        public int BufferedSize
        {
            get
            {
#if MULTITHREAD_SLOW_AND_SAFE
            lock (_Buffer)
            { 
#endif
                return _BufferedSize;
#if MULTITHREAD_SLOW_AND_SAFE
            }
#endif
            }
        }

        /// <remarks>
        /// this is thread safe. But it is strongly recommended to read in only one thread.
        /// If not, the data read can be uncompleted (a part is read by this thread, and other parts are read by other threads).
        /// </remarks>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, -1);
        }
        protected int Read(byte[] buffer, int offset, int count, int remainBytesInTail)
        {
            if (_Closed)
            {
                return 0;
            }
            while (true)
            {
                if (_Timeout < 0)
                {
                    while (!_DataReady.WaitOne(CONST.MAX_WAIT_MILLISECONDS))
                    {
                        if (_Closed)
                        {
                            _DataReady.Set();
                            return 0;
                        }
                    }
                }
                else if (_Timeout <= CONST.MAX_WAIT_MILLISECONDS)
                {
                    if (!_DataReady.WaitOne(_Timeout))
                    {
                        return 0;
                    }
                }
                else
                {
                    var timeout = _Timeout;
                    var part = CONST.MAX_WAIT_MILLISECONDS;
                    while (timeout > 0 && !_DataReady.WaitOne(part))
                    {
                        if (_Closed)
                        {
                            _DataReady.Set();
                            return 0;
                        }
                        timeout -= part;
                        part = Math.Min(timeout, CONST.MAX_WAIT_MILLISECONDS);
                    }
                    if (timeout == 0)
                    {
                        return 0;
                    }
                }
                if (_Closed)
                {
                    _DataReady.Set();
                    return 0;
                }
#if MULTITHREAD_SLOW_AND_SAFE
            lock (_Buffer)
            { 
#endif
                BufferInfo binfo;
                int rcnt = 0;
                while (rcnt < count && _Buffer.TryPeek(out binfo))
                {
                    var consumed = _ReadingHeadConsumed;
                    var binfolen = binfo.Count;
                    var prcnt = binfolen - consumed;
                    if (prcnt <= 0)
                    {
                        continue; // the binfo is used up, and it's being dequeued by another thread.
                    }
                    bool readlessthanbuffer = rcnt + prcnt > count;
                    if (readlessthanbuffer)
                    {
                        prcnt = count - rcnt;
                    }
                    int bsize = Volatile.Read(ref _BufferedSize);
                    if (remainBytesInTail >= 0)
                    {
                        if (bsize <= remainBytesInTail)
                        {
                            break;
                        }
                        prcnt = Math.Min(prcnt, bsize - remainBytesInTail);
                    }

                    if (Interlocked.CompareExchange(ref _ReadingHeadConsumed, consumed + prcnt, consumed) != consumed)
                    {
                        continue; // another thread read from the buffer. this thread should try again.
                    }

                    bsize = _BufferedSize;
                    int nbsize;
                    SpinWait spin = new SpinWait();
                    while (bsize != (nbsize = Interlocked.CompareExchange(ref _BufferedSize, bsize - prcnt, bsize)))
                    {
                        spin.SpinOnce();
                        bsize = nbsize;
                    }
                    bsize -= rcnt;
                    if (remainBytesInTail >= 0)
                    {
                        if (bsize <= remainBytesInTail)
                        {
                            break;
                        }
                    }

                    if (buffer != null)
                    {
                        Buffer.BlockCopy(binfo.Buffer.Buffer, consumed, buffer, offset + rcnt, prcnt);
                    }
                    if (!readlessthanbuffer)
                    { // need to dequeue.
                        Interlocked.Exchange(ref _ReadingHeadConsumed, int.MaxValue);
                        while (_Buffer.TryDequeue(out binfo))
                        {
                            binfo.Buffer.Release();
                            if (!_Buffer.TryPeek(out binfo) || binfo.Count > 0)
                            {
                                break;
                            }
                        }
                        Interlocked.Exchange(ref _ReadingHeadConsumed, 0);
                    }
                    rcnt += prcnt;
                }
                if (Volatile.Read(ref _BufferedSize) > 0)
                {
                    _DataReady.Set();
                }
                if (rcnt > 0)
                {
                    return rcnt;
                }
                if (remainBytesInTail >= 0)
                {
                    return rcnt;
                }
#if MULTITHREAD_SLOW_AND_SAFE
            }
#endif
            }
        }
        /// <remarks>
        /// this is thread safe. But it is strongly recommended to write in only one thread.
        /// If not, the data written may be staggered.
        /// </remarks>
        public override void Write(byte[] buffer, int offset, int count)
        {
#if MULTITHREAD_SLOW_AND_SAFE
            lock (_Buffer)
            { 
#endif
            if (count > 0)
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

                    int bsize = _BufferedSize;
                    int nbsize;
                    SpinWait spin = new SpinWait();
                    while (bsize != (nbsize = Interlocked.CompareExchange(ref _BufferedSize, bsize + scnt, bsize)))
                    {
                        spin.SpinOnce();
                        bsize = nbsize;
                    }

                    cntwrote += scnt;
                }

            }
            _DataReady.Set();
#if MULTITHREAD_SLOW_AND_SAFE
            }
#endif
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _Closed = true;
            _DataReady.Set();
        }
    }
}

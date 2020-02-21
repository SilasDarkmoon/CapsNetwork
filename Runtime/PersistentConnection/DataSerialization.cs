using System;
using System.IO;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public abstract class DataSplitterFactory
    {
        public abstract DataSplitter Create(Stream input);
    }

    public abstract class DataSplitter : IDisposable
    {
        protected sealed class DataSplitterFactory<T> : DataSplitterFactory
            where T : DataSplitter, new()
        {
            public override DataSplitter Create(Stream input)
            {
                var inst = new T();
                inst.Attach(input);
                return inst;
            }
        }
        protected virtual void Attach(Stream input)
        {
            _InputStream = input;
            _BufferedStream = input as IBuffered;
            var inotify = input as INotifyReceiveStream;
            if (inotify != null)
            {
                inotify.OnReceive += OnReceiveData;
            }
        }
        protected void OnReceiveData(byte[] data, int offset, int cnt)
        {
            while (TryReadBlock()) ;
        }

        protected Stream _InputStream;
        protected IBuffered _BufferedStream;

        public abstract void ReadBlock(); // Blocked Read.
        public abstract bool TryReadBlock(); // Non-blocked Read.

        public delegate void ReceiveBlockDelegate(NativeBufferStream buffer, int size, uint type, uint flags, uint seq, uint sseq);
        public event ReceiveBlockDelegate OnReceiveBlock = (buffer, size, type, flags, seq, sseq) => { };

        protected void FireReceiveBlock(NativeBufferStream buffer, int size, uint type, uint flags, uint seq, uint sseq)
        {
#if DEBUG_PERSIST_CONNECT
            PlatDependant.LogInfo(string.Format("Data Received, length {0}, type {1}, flags {2:x}, seq {3}, sseq {4}. (from {5})", size, type, flags, seq, sseq, this.GetType().Name));
#endif
            //buffer.Seek(0, SeekOrigin.Begin);
            OnReceiveBlock(buffer, size, type, flags, seq, sseq);
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            var inotify = _InputStream as INotifyReceiveStream;
            if (inotify != null)
            {
                inotify.OnReceive -= OnReceiveData;
            }
            _InputStream = null;
            _BufferedStream = null;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    public abstract class DataComposer
    {
        public abstract void PrepareBlock(NativeBufferStream data, uint type, uint flags, uint seq, uint sseq);
    }

    public abstract class DataPostProcess
    {
        public virtual uint Process(NativeBufferStream data, int offset, uint flags, uint type, uint seq, uint sseq, bool isServer)
        {
            return flags;
        }
        public virtual Pack<uint, int> Deprocess(NativeBufferStream data, int offset, int cnt, uint flags, uint type, uint seq, uint sseq, bool isServer)
        {
            return new Pack<uint, int>(flags, cnt);
        }
        public abstract int Order { get; }
    }

    public abstract class DataReaderAndWriter
    {
        public abstract uint GetDataType(object data);
        public abstract NativeBufferStream Write(object data);
        public abstract object Read(uint type, NativeBufferStream buffer, int offset, int cnt);
    }
}
using System;
using System.Collections.Generic;
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
        public virtual void Attach(Stream input)
        {
            _InputStream = input;
            _BufferedStream = input as IBuffered;
            var inotify = input as INotifyReceiveStream;
            if (inotify != null)
            {
                inotify.OnReceive += OnReceiveData;
            }
        }
        public virtual void OnReceiveData(byte[] data, int offset, int cnt)
        {
            while (TryReadBlock()) ;
        }

        protected Stream _InputStream;
        protected IBuffered _BufferedStream;

        public abstract void ReadBlock(); // Blocked Read.
        public abstract bool TryReadBlock(); // Non-blocked Read.

        public delegate void ReceiveBlockDelegate(InsertableStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags);
        public event ReceiveBlockDelegate OnReceiveBlock = (buffer, size, type, flags, seq, sseq, exflags) => { };

        protected virtual void FireReceiveBlock(InsertableStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
#if DEBUG_PERSIST_CONNECT
            PlatDependant.LogInfo(string.Format("Data Received, length {0}, type {1}, flags {2:x}, seq {3}, sseq {4}. (from {5})", size, type, flags, seq, sseq, this.GetType().Name));
#endif
            //buffer.Seek(0, SeekOrigin.Begin);
            OnReceiveBlock(buffer, size, type, flags, seq, sseq, exFlags);
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
    public abstract class DataSplitter<T> : DataSplitter where T : DataSplitter<T>, new()
    {
        public static readonly DataSplitterFactory Factory = new DataSplitterFactory<T>();
    }

    public abstract class DataComposer
    {
        public abstract void PrepareBlock(InsertableStream data, uint type, uint flags, uint seq, uint sseq, object exFlags);
    }

    public abstract class DataPostProcess
    {
        public virtual uint Process(InsertableStream data, int offset, uint flags, uint type, uint seq, uint sseq, bool isServer, object exFlags)
        {
            return flags;
        }
        public virtual Pack<uint, int> Deprocess(InsertableStream data, int offset, int cnt, uint flags, uint type, uint seq, uint sseq, bool isServer, object exFlags)
        {
            return new Pack<uint, int>(flags, cnt);
        }
        public abstract int Order { get; }
    }

    public abstract class DataFormatterFactory
    {
        public abstract DataFormatter Create(IChannel connection);
    }

    public abstract class DataFormatter
    {
        protected Dictionary<uint, Func<uint, InsertableStream, int, int, object>> _TypedReaders = new Dictionary<uint, Func<uint, InsertableStream, int, int, object>>(PredefinedMessages.PredefinedReaders);
        protected Dictionary<Type, Func<object, InsertableStream>> _TypedWriters = new Dictionary<Type, Func<object, InsertableStream>>(PredefinedMessages.PredefinedWriters);
        protected Dictionary<Type, uint> _TypeToID = new Dictionary<Type, uint>(PredefinedMessages.PredefinedTypeToID);

        public virtual object GetExFlags(object data)
        {
            return null;
        }
        public virtual uint GetDataType(object data)
        {
            if (data == null)
            {
                return 0;
            }
            else if (data is PredefinedMessages.Unknown)
            {
                return ((PredefinedMessages.Unknown)data).TypeID;
            }
            uint rv;
            _TypeToID.TryGetValue(data.GetType(), out rv);
            return rv;
        }
        public virtual InsertableStream Write(object data)
        {
            if (data == null)
            {
                return null;
            }
            Func<object, InsertableStream> writer;
            if (_TypedWriters.TryGetValue(data.GetType(), out writer))
            {
                return writer(data);
            }
            return null;
        }
        public virtual bool CanWrite(object data)
        {
            if (data == null)
            {
                return false;
            }
            Func<object, InsertableStream> writer;
            if (_TypedWriters.TryGetValue(data.GetType(), out writer))
            {
                return true;
            }
            return false;
        }
        public virtual bool IsOrdered(object data)
        {
            if (data == null)
            {
                return false;
            }
            Func<object, InsertableStream> writer;
            if (_TypedWriters.TryGetValue(data.GetType(), out writer))
            {
                return true;
            }
            return false;
        }
        public virtual object Read(uint type, InsertableStream buffer, int offset, int cnt, object exFlags)
        {
            Func<uint, InsertableStream, int, int, object> reader;
            if (_TypedReaders.TryGetValue(type, out reader))
            {
                return reader(type, buffer, offset, cnt);
            }
            return null;
        }

        public virtual object ReadOrUnknown(uint type, InsertableStream buffer, int offset, int cnt, object exFlags)
        {
            return Read(type, buffer, offset, cnt, exFlags) ?? PredefinedMessages.ReadUnknown(type, buffer, offset, cnt);
        }
    }
}
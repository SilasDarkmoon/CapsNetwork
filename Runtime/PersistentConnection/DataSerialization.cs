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

        public delegate void ReceiveBlockDelegate(NativeBufferStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags);
        public event ReceiveBlockDelegate OnReceiveBlock = (buffer, size, type, flags, seq, sseq, exflags) => { };

        protected virtual void FireReceiveBlock(NativeBufferStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags)
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

    public abstract class DataComposer
    {
        public abstract void PrepareBlock(NativeBufferStream data, uint type, uint flags, uint seq, uint sseq, object exFlags);
    }

    public abstract class DataPostProcess
    {
        public virtual uint Process(NativeBufferStream data, int offset, uint flags, uint type, uint seq, uint sseq, bool isServer, object exFlags)
        {
            return flags;
        }
        public virtual Pack<uint, int> Deprocess(NativeBufferStream data, int offset, int cnt, uint flags, uint type, uint seq, uint sseq, bool isServer, object exFlags)
        {
            return new Pack<uint, int>(flags, cnt);
        }
        public abstract int Order { get; }
    }

    public abstract class DataReaderAndWriter
    {
        protected Dictionary<uint, Func<uint, NativeBufferStream, int, int, object>> _TypedReaders = new Dictionary<uint, Func<uint, NativeBufferStream, int, int, object>>(PredefinedMessages.PredefinedReaders);
        protected Dictionary<Type, Func<object, NativeBufferStream>> _TypedWriters = new Dictionary<Type, Func<object, NativeBufferStream>>(PredefinedMessages.PredefinedWriters);
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
            uint rv;
            _TypeToID.TryGetValue(data.GetType(), out rv);
            return rv;
        }
        public virtual NativeBufferStream Write(object data)
        {
            if (data == null)
            {
                return null;
            }
            Func<object, NativeBufferStream> writer;
            if (_TypedWriters.TryGetValue(data.GetType(), out writer))
            {
                return writer(data);
            }
            return null;
        }
        public virtual object Read(uint type, NativeBufferStream buffer, int offset, int cnt)
        {
            Func<uint, NativeBufferStream, int, int, object> reader;
            if (_TypedReaders.TryGetValue(type, out reader))
            {
                return reader(type, buffer, offset, cnt);
            }
            return null;
        }
    }

    public static class PredefinedMessages
    {
        public static Dictionary<uint, Func<uint, NativeBufferStream, int, int, object>> PredefinedReaders = new Dictionary<uint, Func<uint, NativeBufferStream, int, int, object>>()
        {
            { Error.TypeID, ReadError },
            { Raw.TypeID, ReadRaw },
            { String.TypeID, ReadString },
            { Integer.TypeID, ReadInteger },
            { Number.TypeID, ReadNumber },
        };
        public static Dictionary<Type, Func<object, NativeBufferStream>> PredefinedWriters = new Dictionary<Type, Func<object, NativeBufferStream>>()
        {
            { typeof(Error), WriteError },
            { typeof(byte[]), WriteRawRaw },
            { typeof(Raw), WriteRaw },
            { typeof(string), WriteRawString },
            { typeof(String), WriteString },
            { typeof(int), WriteRawInt32 },
            { typeof(uint), WriteRawUInt32 },
            { typeof(long), WriteRawInt64 },
            { typeof(ulong), WriteRawUInt64 },
            { typeof(IntPtr), WriteRawIntPtr },
            { typeof(UIntPtr), WriteRawUIntPtr },
            { typeof(Integer), WriteInteger },
            { typeof(float), WriteRawFloat },
            { typeof(double), WriteRawDouble },
            { typeof(Number), WriteNumber },
        };
        public static Dictionary<Type, uint> PredefinedTypeToID = new Dictionary<Type, uint>()
        {
            { typeof(Error), Error.TypeID },
            { typeof(byte[]), Raw.TypeID },
            { typeof(Raw), Raw.TypeID },
            { typeof(string), String.TypeID },
            { typeof(String), String.TypeID },
            { typeof(int), Integer.TypeID },
            { typeof(uint), Integer.TypeID },
            { typeof(long), Integer.TypeID },
            { typeof(ulong), Integer.TypeID },
            { typeof(IntPtr), Integer.TypeID },
            { typeof(UIntPtr), Integer.TypeID },
            { typeof(Integer), Integer.TypeID },
            { typeof(float), Number.TypeID },
            { typeof(double), Number.TypeID },
            { typeof(Number), Number.TypeID },
        };
        public static Dictionary<uint, Type> PredefinedIDToType = new Dictionary<uint, Type>()
        {
            { Error.TypeID, typeof(Error) },
            { Raw.TypeID, typeof(Raw) },
            { String.TypeID, typeof(String) },
            { Integer.TypeID, typeof(Integer) },
            { Number.TypeID, typeof(Number) },
        };

        [ThreadStatic] private static NativeBufferStream _CommonWriterBuffer;
        private static NativeBufferStream CommonWriterBuffer
        {
            get
            {
                var buffer = _CommonWriterBuffer;
                if (buffer == null)
                {
                    _CommonWriterBuffer = buffer = new NativeBufferStream();
                }
                return buffer;
            }
        }
        [ThreadStatic] private static IPooledBuffer _RawBuffer;
        private static byte[] GetRawBuffer(int cnt)
        {
            var buffer = _RawBuffer;
            if (buffer == null)
            {
                _RawBuffer = buffer = BufferPool.GetBufferFromPool();
            }
            if (buffer != null)
            {
                if (buffer.Length >= cnt)
                {
                    return buffer.Buffer;
                }
                else
                {
                    buffer.Release();
                    _RawBuffer = buffer = null;
                }
            }
            _RawBuffer = buffer = BufferPool.GetBufferFromPool(cnt);
            return buffer.Buffer;
        }

        public static object ReadError(uint type, NativeBufferStream buffer, int offset, int cnt)
        {
            if (type != Error.TypeID)
            {
                PlatDependant.LogError("ReadError - not an error - type " + type);
                return null;
            }
            try
            {
                byte[] raw = GetRawBuffer(cnt);
                buffer.Seek(offset, SeekOrigin.Begin);
                buffer.Read(raw, 0, cnt);
                string str = null;
                try
                {
                    str = System.Text.Encoding.UTF8.GetString(raw);
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
                return new Error() { Message = str };
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return null;
            }
        }
        public static NativeBufferStream WriteError(object data)
        {
            var real = data as Error;
            if (real == null)
            {
                PlatDependant.LogError("WriteError - not an error - " + data);
                return null;
            }
            return WriteRawString(real.Message);
        }

        public static object ReadRaw(uint type, NativeBufferStream buffer, int offset, int cnt)
        {
            if (type != Raw.TypeID)
            {
                PlatDependant.LogError("ReadRaw - not a raw - type " + type);
                return null;
            }
            try
            {
                byte[] raw = new byte[cnt]; // because this is exposed to outter caller, so we new the raw buffer.
                buffer.Seek(offset, SeekOrigin.Begin);
                buffer.Read(raw, 0, cnt);
                return new Raw() { Message = raw };
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return null;
            }
        }
        private static byte[] _EmptyData = new byte[0];
        public static NativeBufferStream WriteRawRaw(object data)
        {
            if (data == null)
            {
                data = _EmptyData;
            }
            var real = data as byte[];
            if (real == null)
            {
                PlatDependant.LogError("WriteRawRaw - not a raw - " + data);
                return null;
            }
            var buffer = CommonWriterBuffer;
            buffer.Clear();
            buffer.Write(real, 0, real.Length);
            return buffer;
        }
        public static NativeBufferStream WriteRaw(object data)
        {
            var real = data as Raw;
            if (real == null)
            {
                PlatDependant.LogError("WriteRaw - not a raw - " + data);
                return null;
            }
            return WriteRawRaw(real.Message);
        }

        public static object ReadString(uint type, NativeBufferStream buffer, int offset, int cnt)
        {
            if (type != String.TypeID)
            {
                PlatDependant.LogError("ReadString - not a string - type " + type);
                return null;
            }
            try
            {
                byte[] raw = GetRawBuffer(cnt);
                buffer.Seek(offset, SeekOrigin.Begin);
                buffer.Read(raw, 0, cnt);
                string str = null;
                try
                {
                    str = System.Text.Encoding.UTF8.GetString(raw);
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
                return new String() { Message = str };
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return null;
            }
        }
        public static NativeBufferStream WriteRawString(object data)
        {
            if (data == null)
            {
                data = "";
            }
            var real = data as string;
            if (real == null)
            {
                PlatDependant.LogError("WriteRawString - not a string - " + data);
                return null;
            }
            var buffer = CommonWriterBuffer;
            buffer.Clear();
            try
            {
                var cnt = System.Text.Encoding.UTF8.GetByteCount(real);
                var raw = GetRawBuffer(cnt);
                System.Text.Encoding.UTF8.GetBytes(real, 0, real.Length, raw, 0);
                buffer.Write(raw, 0, cnt);
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
            }
            return buffer;
        }
        public static NativeBufferStream WriteString(object data)
        {
            var real = data as String;
            if (real == null)
            {
                PlatDependant.LogError("WriteString - not a string - " + data);
                return null;
            }
            return WriteRawString(real.Message);
        }

        public static object ReadInteger(uint type, NativeBufferStream buffer, int offset, int cnt)
        {
            if (type != Integer.TypeID)
            {
                PlatDependant.LogError("ReadInteger - not an integer - type " + type);
                return null;
            }
            try
            {
                byte[] raw = GetRawBuffer(cnt);
                buffer.Seek(offset, SeekOrigin.Begin);
                buffer.Read(raw, 0, cnt);
                long value = 0;
                for (int i = 0; i < cnt && i < 8; ++i)
                {
                    long part = raw[i];
                    value += part << (8 * i);
                }
                return new Integer() { Message = value };
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return null;
            }
        }
        public static NativeBufferStream WriteRawInt32(object data)
        {
            if (data is int)
            {
                int value = (int)data;
                var buffer = CommonWriterBuffer;
                buffer.Clear();
                buffer.WriteByte((byte)(value & 0xFF));
                buffer.WriteByte((byte)((value & (0xFF << 8)) >> 8));
                buffer.WriteByte((byte)((value & (0xFF << 16)) >> 16));
                buffer.WriteByte((byte)((value & (0xFF << 24)) >> 24));
                return buffer;
            }
            else
            {
                PlatDependant.LogError("WriteRawInt32 - not an Int32 - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteRawUInt32(object data)
        {
            if (data is uint)
            {
                uint value = (uint)data;
                var buffer = CommonWriterBuffer;
                buffer.Clear();
                buffer.WriteByte((byte)(value & 0xFF));
                buffer.WriteByte((byte)((value & (0xFF << 8)) >> 8));
                buffer.WriteByte((byte)((value & (0xFF << 16)) >> 16));
                buffer.WriteByte((byte)((value & (0xFF << 24)) >> 24));
                return buffer;
            }
            else
            {
                PlatDependant.LogError("WriteRawUInt32 - not an UInt32 - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteRawInt64(object data)
        {
            if (data is long)
            {
                long value = (long)data;
                var buffer = CommonWriterBuffer;
                buffer.Clear();
                buffer.WriteByte((byte)(value & 0xFFL));
                buffer.WriteByte((byte)((value & (0xFFL << 8)) >> 8));
                buffer.WriteByte((byte)((value & (0xFFL << 16)) >> 16));
                buffer.WriteByte((byte)((value & (0xFFL << 24)) >> 24));
                buffer.WriteByte((byte)((value & (0xFFL << 32)) >> 32));
                buffer.WriteByte((byte)((value & (0xFFL << 40)) >> 40));
                buffer.WriteByte((byte)((value & (0xFFL << 48)) >> 48));
                buffer.WriteByte((byte)((value & (0xFFL << 56)) >> 56));
                return buffer;
            }
            else
            {
                PlatDependant.LogError("WriteRawInt64 - not an Int64 - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteRawUInt64(object data)
        {
            if (data is ulong)
            {
                ulong value = (ulong)data;
                var buffer = CommonWriterBuffer;
                buffer.Clear();
                buffer.WriteByte((byte)(value & 0xFFUL));
                buffer.WriteByte((byte)((value & (0xFFUL << 8)) >> 8));
                buffer.WriteByte((byte)((value & (0xFFUL << 16)) >> 16));
                buffer.WriteByte((byte)((value & (0xFFUL << 24)) >> 24));
                buffer.WriteByte((byte)((value & (0xFFUL << 32)) >> 32));
                buffer.WriteByte((byte)((value & (0xFFUL << 40)) >> 40));
                buffer.WriteByte((byte)((value & (0xFFUL << 48)) >> 48));
                buffer.WriteByte((byte)((value & (0xFFUL << 56)) >> 56));
                return buffer;
            }
            else
            {
                PlatDependant.LogError("WriteRawUInt64 - not an UInt64 - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteRawIntPtr(object data)
        {
            if (data is IntPtr)
            {
                ulong value = (ulong)(IntPtr)data;
                var buffer = CommonWriterBuffer;
                buffer.Clear();
                buffer.WriteByte((byte)(value & 0xFFUL));
                buffer.WriteByte((byte)((value & (0xFFUL << 8)) >> 8));
                buffer.WriteByte((byte)((value & (0xFFUL << 16)) >> 16));
                buffer.WriteByte((byte)((value & (0xFFUL << 24)) >> 24));
                if (IntPtr.Size >= 8)
                {
                    buffer.WriteByte((byte)((value & (0xFFUL << 32)) >> 32));
                    buffer.WriteByte((byte)((value & (0xFFUL << 40)) >> 40));
                    buffer.WriteByte((byte)((value & (0xFFUL << 48)) >> 48));
                    buffer.WriteByte((byte)((value & (0xFFUL << 56)) >> 56));
                }
                return buffer;
            }
            else
            {
                PlatDependant.LogError("WriteRawIntPtr - not an IntPtr - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteRawUIntPtr(object data)
        {
            if (data is UIntPtr)
            {
                ulong value = (ulong)(UIntPtr)data;
                var buffer = CommonWriterBuffer;
                buffer.Clear();
                buffer.WriteByte((byte)(value & 0xFFUL));
                buffer.WriteByte((byte)((value & (0xFFUL << 8)) >> 8));
                buffer.WriteByte((byte)((value & (0xFFUL << 16)) >> 16));
                buffer.WriteByte((byte)((value & (0xFFUL << 24)) >> 24));
                if (UIntPtr.Size >= 8)
                {
                    buffer.WriteByte((byte)((value & (0xFFUL << 32)) >> 32));
                    buffer.WriteByte((byte)((value & (0xFFUL << 40)) >> 40));
                    buffer.WriteByte((byte)((value & (0xFFUL << 48)) >> 48));
                    buffer.WriteByte((byte)((value & (0xFFUL << 56)) >> 56));
                }
                return buffer;
            }
            else
            {
                PlatDependant.LogError("WriteRawUIntPtr - not an UIntPtr - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteInteger(object data)
        {
            var real = data as Integer;
            if (real == null)
            {
                PlatDependant.LogError("WriteInteger - not an integer - " + data);
                return null;
            }
            return WriteRawInt64(real.Message);
        }

        public static object ReadNumber(uint type, NativeBufferStream buffer, int offset, int cnt)
        {
            if (type != Number.TypeID)
            {
                PlatDependant.LogError("ReadNumber - not a number - type " + type);
                return null;
            }
            try
            {
                byte[] raw = GetRawBuffer(8);
                for (int i = 0; i < 8; ++i)
                {
                    raw[i] = 0;
                }
                buffer.Seek(offset, SeekOrigin.Begin);
                buffer.Read(raw, 0, cnt);
                if (!BitConverter.IsLittleEndian)
                {
                    for (int i = 0; i < 4; ++i)
                    {
                        byte temp = raw[i];
                        raw[i] = raw[7 - i];
                        raw[7 - i] = temp;
                    }
                }
                double value = BitConverter.ToDouble(raw, 0);
                return new Number() { Message = value };
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return null;
            }
        }
        public static NativeBufferStream WriteRawFloat(object data)
        {
            if (data is float)
            {
                float value = (float)data;
                var raw = BitConverter.GetBytes(value);
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(raw);
                }
                return WriteRawRaw(raw);
            }
            else
            {
                PlatDependant.LogError("WriteRawFloat - not a Float - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteRawDouble(object data)
        {
            if (data is double)
            {
                long value = BitConverter.DoubleToInt64Bits((double)data);
                var buffer = CommonWriterBuffer;
                buffer.Clear();
                buffer.WriteByte((byte)(value & 0xFFL));
                buffer.WriteByte((byte)((value & (0xFFL << 8)) >> 8));
                buffer.WriteByte((byte)((value & (0xFFL << 16)) >> 16));
                buffer.WriteByte((byte)((value & (0xFFL << 24)) >> 24));
                buffer.WriteByte((byte)((value & (0xFFL << 32)) >> 32));
                buffer.WriteByte((byte)((value & (0xFFL << 40)) >> 40));
                buffer.WriteByte((byte)((value & (0xFFL << 48)) >> 48));
                buffer.WriteByte((byte)((value & (0xFFL << 56)) >> 56));
                return buffer;
            }
            else
            {
                PlatDependant.LogError("WriteRawDouble - not a Double - " + data);
                return null;
            }
        }
        public static NativeBufferStream WriteNumber(object data)
        {
            var real = data as Number;
            if (real == null)
            {
                PlatDependant.LogError("WriteNumber - not a number - " + data);
                return null;
            }
            return WriteRawDouble(real.Message);
        }

        public class Error
        {
            public const uint TypeID = unchecked((uint)-1);
            public string Message;
        }
        public class Raw
        {
            public const uint TypeID = unchecked((uint)-2);
            public byte[] Message;
        }
        public class String
        {
            public const uint TypeID = unchecked((uint)-3);
            public string Message;
        }
        public class Integer
        {
            public const uint TypeID = unchecked((uint)-4);
            public long Message;
        }
        public class Number
        {
            public const uint TypeID = unchecked((uint)-5);
            public double Message;
        }

        private static Raw _Empty = new Raw();
        public static Raw Empty { get { return _Empty; } }
        private static object _NoResponse = new object();
        public static object NoResponse { get { return _NoResponse; } }
    }
}
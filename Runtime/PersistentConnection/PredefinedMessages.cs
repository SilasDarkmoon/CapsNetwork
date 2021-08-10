using System;
using System.Collections.Generic;
using System.IO;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public static class PredefinedMessages
    {
        public static Dictionary<uint, Func<uint, InsertableStream, int, int, object>> PredefinedReaders = new Dictionary<uint, Func<uint, InsertableStream, int, int, object>>()
        {
            { Error.TypeID, ReadError },
            { Raw.TypeID, ReadRaw },
            { String.TypeID, ReadString },
            { Integer.TypeID, ReadInteger },
            { Number.TypeID, ReadNumber },
            { Control.TypeID, ReadControl },
        };
        public static Dictionary<Type, Func<object, InsertableStream>> PredefinedWriters = new Dictionary<Type, Func<object, InsertableStream>>()
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
            { typeof(Control), WriteControl },
            { typeof(Unknown), WriteUnknown },
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
            { typeof(Control), Control.TypeID },
        };
        public static Dictionary<uint, Type> PredefinedIDToType = new Dictionary<uint, Type>()
        {
            { Error.TypeID, typeof(Error) },
            { Raw.TypeID, typeof(Raw) },
            { String.TypeID, typeof(String) },
            { Integer.TypeID, typeof(Integer) },
            { Number.TypeID, typeof(Number) },
            { Control.TypeID, typeof(Control) },
        };

        [ThreadStatic] private static InsertableStream _CommonWriterBuffer;
        private static InsertableStream CommonWriterBuffer
        {
            get
            {
                var buffer = _CommonWriterBuffer;
                if (buffer == null)
                {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
                    _CommonWriterBuffer = buffer = new NativeBufferStream();
#else
                    _CommonWriterBuffer = buffer = new ArrayBufferStream();
#endif
                }
                return buffer;
            }
        }
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        [ThreadStatic] private static IPooledBuffer _RawBuffer;
        public static byte[] GetRawBuffer(int cnt)
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
#else
        public static byte[] GetRawBuffer(int cnt)
        {
            return new byte[cnt];
        }
#endif

        public static object ReadError(uint type, InsertableStream buffer, int offset, int cnt)
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
                    str = System.Text.Encoding.UTF8.GetString(raw, 0, cnt);
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
        public static InsertableStream WriteError(object data)
        {
            var real = data as Error;
            if (real == null)
            {
                PlatDependant.LogError("WriteError - not an error - " + data);
                return null;
            }
            return WriteRawString(real.Message);
        }

        public static object ReadRaw(uint type, InsertableStream buffer, int offset, int cnt)
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
        public static InsertableStream WriteRawRaw(object data)
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
        public static InsertableStream WriteRaw(object data)
        {
            var real = data as Raw;
            if (real == null)
            {
                PlatDependant.LogError("WriteRaw - not a raw - " + data);
                return null;
            }
            return WriteRawRaw(real.Message);
        }

        public static object ReadString(uint type, InsertableStream buffer, int offset, int cnt)
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
                    str = System.Text.Encoding.UTF8.GetString(raw, 0, cnt);
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
        public static InsertableStream WriteRawString(object data)
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
        public static InsertableStream WriteString(object data)
        {
            var real = data as String;
            if (real == null)
            {
                PlatDependant.LogError("WriteString - not a string - " + data);
                return null;
            }
            return WriteRawString(real.Message);
        }

        public static object ReadInteger(uint type, InsertableStream buffer, int offset, int cnt)
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
        public static InsertableStream WriteRawInt32(object data)
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
        public static InsertableStream WriteRawUInt32(object data)
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
        public static InsertableStream WriteRawInt64(object data)
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
        public static InsertableStream WriteRawUInt64(object data)
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
        public static InsertableStream WriteRawIntPtr(object data)
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
        public static InsertableStream WriteRawUIntPtr(object data)
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
        public static InsertableStream WriteInteger(object data)
        {
            var real = data as Integer;
            if (real == null)
            {
                PlatDependant.LogError("WriteInteger - not an integer - " + data);
                return null;
            }
            return WriteRawInt64(real.Message);
        }

        public static object ReadNumber(uint type, InsertableStream buffer, int offset, int cnt)
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
        public static InsertableStream WriteRawFloat(object data)
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
        public static InsertableStream WriteRawDouble(object data)
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
        public static InsertableStream WriteNumber(object data)
        {
            var real = data as Number;
            if (real == null)
            {
                PlatDependant.LogError("WriteNumber - not a number - " + data);
                return null;
            }
            return WriteRawDouble(real.Message);
        }

        public static object ReadControl(uint type, InsertableStream buffer, int offset, int cnt)
        {
            if (type != Control.TypeID)
            {
                PlatDependant.LogError("ReadControl - not a control - type " + type);
                return null;
            }
            try
            {
                byte[] raw = GetRawBuffer(4);
                for (int i = 0; i < 4; ++i)
                {
                    raw[i] = 0;
                }
                buffer.Seek(offset, SeekOrigin.Begin);
                buffer.Read(raw, 0, Math.Min(4, cnt));
                if (!BitConverter.IsLittleEndian)
                {
                    for (int i = 0; i < 2; ++i)
                    {
                        byte temp = raw[i];
                        raw[i] = raw[3 - i];
                        raw[3 - i] = temp;
                    }
                }
                uint code = BitConverter.ToUInt32(raw, 0);
                int remain = cnt - 4;
                remain = Math.Max(remain, 0);
                raw = GetRawBuffer(remain);
                buffer.Read(raw, 0, remain);
                string str = null;
                try
                {
                    str = System.Text.Encoding.UTF8.GetString(raw, 0, remain);
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
                return new Control() { Code = code, Command = str };
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return null;
            }
        }
        public static InsertableStream WriteControl(object data)
        {
            var real = data as Control;
            if (real == null)
            {
                PlatDependant.LogError("WriteControl - not a control - " + data);
                return null;
            }
            var buffer = CommonWriterBuffer;
            buffer.Clear();
            var code = real.Code;
            buffer.WriteByte((byte)(code & 0xFFU));
            buffer.WriteByte((byte)((code & (0xFFU << 8)) >> 8));
            buffer.WriteByte((byte)((code & (0xFFU << 16)) >> 16));
            buffer.WriteByte((byte)((code & (0xFFU << 24)) >> 24));
            var command = real.Command;
            if (command == null)
            {
                command = "";
            }
            try
            {
                var cnt = System.Text.Encoding.UTF8.GetByteCount(command);
                var raw = GetRawBuffer(cnt);
                System.Text.Encoding.UTF8.GetBytes(command, 0, command.Length, raw, 0);
                buffer.Write(raw, 0, cnt);
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
            }
            return buffer;
        }

        public static object ReadUnknown(uint type, InsertableStream buffer, int offset, int cnt)
        {
            try
            {
                byte[] raw = new byte[cnt]; // because this is exposed to outter caller, so we new the raw buffer.
                buffer.Seek(offset, SeekOrigin.Begin);
                buffer.Read(raw, 0, cnt);
                return new Unknown() { TypeID = type, Message = raw };
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                return null;
            }
        }
        public static InsertableStream WriteUnknown(object data)
        {
            var real = data as Unknown;
            if (real == null)
            {
                PlatDependant.LogError("WriteUnknown - not a Unknown - " + data);
                return null;
            }
            return WriteRawRaw(real.Message);
        }

        public class Error
        {
            public const uint TypeID = unchecked((uint)-1);
            public string Message;

            public Error() { }
            public Error(string message) { Message = message; }

            public override string ToString()
            {
                return "(Error)" + (Message ?? "null");
            }
        }
        public class Raw
        {
            public const uint TypeID = unchecked((uint)-2);
            public byte[] Message;

            public Raw() { }
            public Raw(byte[] message) { Message = message; }

            public override string ToString()
            {
                return "(Raw)" + (Message == null ? "null" : Message.Length.ToString());
            }
        }
        public class String
        {
            public const uint TypeID = unchecked((uint)-3);
            public string Message;

            public String() { }
            public String(string message) { Message = message; }

            public override string ToString()
            {
                return Message;
            }
        }
        public class Integer
        {
            public const uint TypeID = unchecked((uint)-4);
            public long Message;

            public Integer() { }
            public Integer(long message) { Message = message; }

            public override string ToString()
            {
                return Message.ToString();
            }
        }
        public class Number
        {
            public const uint TypeID = unchecked((uint)-5);
            public double Message;

            public Number() { }
            public Number(double message) { Message = message; }

            public override string ToString()
            {
                return Message.ToString();
            }
        }
        public class Control
        {
            public const uint TypeID = unchecked((uint)-6);
            public uint Code;
            public string Command;

            public Control() { }
            public Control(uint code) { Code = code; }
            public Control(string command) { Command = command; }
            public Control(uint code, string command) : this(code) { Command = command; }

            public override string ToString()
            {
                return "(Code)" + Code + ", (Command)" + (Command ?? "null");
            }
        }
        public class Unknown
        {
            public uint TypeID;
            public byte[] Message;

            public Unknown() { }
            public Unknown(uint type, byte[] message) { TypeID = type; Message = message; }

            public override string ToString()
            {
                return "(Type)" + TypeID + ", (Unknown)" + (Message == null ? "null" : Message.Length.ToString());
            }
        }

        private static Raw _Empty = new Raw();
        public static Raw Empty { get { return _Empty; } }
        private static object _NoResponse = new object();
        public static object NoResponse { get { return _NoResponse; } }
    }
}
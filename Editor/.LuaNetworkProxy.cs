using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Capstones.UnityEngineEx;

using Capstones.LuaExt;
using Capstones.LuaLib;
using Capstones.LuaWrap;
using lua = Capstones.LuaLib.LuaCoreLib;
using lual = Capstones.LuaLib.LuaAuxLib;
using luae = Capstones.LuaLib.LuaLibEx;
using static Capstones.LuaWrap.LuaPack;

#region Lua Composer and Splitter
namespace Capstones.Net
{
    public static class LuaNetProxyUtils
    {
        public static uint ReadUIntBigEndian(Stream stream)
        {
            uint val = 0;
            for (int i = 0; i < 4; ++i)
            {
                val <<= 8;
                val += (byte)stream.ReadByte();
            }
            return val;
        }
        public static int ReadIntBigEndian(Stream stream)
        {
            return (int)ReadUIntBigEndian(stream);
        }
        public static ushort ReadUShortBigEndian(Stream stream)
        {
            ushort val = 0;
            for (int i = 0; i < 2; ++i)
            {
                val <<= 8;
                val += (byte)stream.ReadByte();
            }
            return val;
        }
        public static short ReadShortBigEndian(Stream stream)
        {
            return (short)ReadUShortBigEndian(stream);
        }
        public static ulong ReadULongBigEndian(Stream stream)
        {
            ulong val = 0;
            for (int i = 0; i < 8; ++i)
            {
                val <<= 8;
                val += (byte)stream.ReadByte();
            }
            return val;
        }
        public static long ReadLongBigEndian(Stream stream)
        {
            return (long)ReadULongBigEndian(stream);
        }

        public static uint ComposeUIntBigEndian(byte b0, byte b1, byte b2, byte b3)
        {
            uint val = 0;
            val += ((uint)b0) << 24;
            val += ((uint)b1) << 16;
            val += ((uint)b2) << 8;
            val += ((uint)b3);
            return val;
        }
        public static int ComposeIntBigEndian(byte b0, byte b1, byte b2, byte b3)
        {
            return (int)ComposeUIntBigEndian(b0, b1, b2, b3);
        }
        public static ushort ComposeUShortBigEndian(byte b0, byte b1)
        {
            ushort val = 0;
            val += (ushort)((b0) << 8);
            val += (ushort)(b1);
            return val;
        }
        public static short ComposeShortBigEndian(byte b0, byte b1)
        {
            return (short)ComposeUShortBigEndian(b0, b1);
        }
        public static ulong ComposeULongBigEndian(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7)
        {
            ulong val = 0;
            val += ((ulong)b0) << 56;
            val += ((ulong)b1) << 48;
            val += ((ulong)b2) << 40;
            val += ((ulong)b3) << 32;
            val += ((ulong)b4) << 24;
            val += ((ulong)b5) << 16;
            val += ((ulong)b6) << 8;
            val += ((ulong)b7);
            return val;
        }
        public static long ComposeLongBigEndian(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7)
        {
            return (long)ComposeULongBigEndian(b0, b1, b2, b3, b4, b5, b6, b7);
        }

        public static void WriteUIntBigEndian(Stream stream, uint val)
        {
            stream.WriteByte((byte)((val >> 24) & 0xFFU));
            stream.WriteByte((byte)((val >> 16) & 0xFFU));
            stream.WriteByte((byte)((val >> 8) & 0xFFU));
            stream.WriteByte((byte)(val & 0xFFU));
        }
        public static void WriteIntBigEndian(Stream stream, int val)
        {
            WriteUIntBigEndian(stream, (uint)val);
        }
        public static void WriteUShortBigEndian(Stream stream, ushort val)
        {
            stream.WriteByte((byte)((val >> 8) & 0xFF));
            stream.WriteByte((byte)(val & 0xFF));
        }
        public static void WriteShortBigEndian(Stream stream, short val)
        {
            WriteUShortBigEndian(stream, (ushort)val);
        }
        public static void WriteULongBigEndian(Stream stream, ulong val)
        {
            stream.WriteByte((byte)((val >> 56) & 0xFFUL));
            stream.WriteByte((byte)((val >> 48) & 0xFFUL));
            stream.WriteByte((byte)((val >> 40) & 0xFFUL));
            stream.WriteByte((byte)((val >> 32) & 0xFFUL));
            stream.WriteByte((byte)((val >> 24) & 0xFFUL));
            stream.WriteByte((byte)((val >> 16) & 0xFFUL));
            stream.WriteByte((byte)((val >> 8) & 0xFFUL));
            stream.WriteByte((byte)(val & 0xFFUL));
        }
        public static void WriteLongBigEndian(Stream stream, long val)
        {
            WriteULongBigEndian(stream, (ulong)val);
        }

        public static byte PartOfUIntBigEndian(uint val, int index)
        {
            return (byte)((val >> ((3 - index) * 8)) & 0xFFU);
        }
        public static byte PartOfIntBigEndian(int val, int index)
        {
            return PartOfUIntBigEndian((uint)val, index);
        }
        public static byte PartOfUShortBigEndian(ushort val, int index)
        {
            return (byte)((val >> ((1 - index) * 8)) & 0xFFU);
        }
        public static byte PartOfShortBigEndian(short val, int index)
        {
            return PartOfUShortBigEndian((ushort)val, index);
        }
        public static byte PartOfULongBigEndian(ulong val, int index)
        {
            return (byte)((val >> ((7 - index) * 8)) & 0xFFUL);
        }
        public static byte PartOfLongBigEndian(long val, int index)
        {
            return PartOfULongBigEndian((ulong) val, index);
        }

        public static (byte, byte, byte, byte) SplitUIntBigEndian(uint val)
        {
            return (
                (byte)((val >> 24) & 0xFFU),
                (byte)((val >> 16) & 0xFFU),
                (byte)((val >> 8) & 0xFFU),
                (byte)(val & 0xFFU)
                );
        }
        public static (byte, byte, byte, byte) SplitIntBigEndian(int val)
        {
            return SplitUIntBigEndian((uint)val);
        }
        public static (byte, byte) SplitUShortBigEndian(ushort val)
        {
            return (
                (byte)((val >> 8) & 0xFF),
                (byte)(val & 0xFF)
                );
        }
        public static (byte, byte) SplitShortBigEndian(short val)
        {
            return SplitUShortBigEndian((ushort)val);
        }
        public static (byte, byte, byte, byte, byte, byte, byte, byte) SplitULongBigEndian(ulong val)
        {
            return (
                (byte)((val >> 56) & 0xFFUL),
                (byte)((val >> 48) & 0xFFUL),
                (byte)((val >> 40) & 0xFFUL),
                (byte)((val >> 32) & 0xFFUL),
                (byte)((val >> 24) & 0xFFUL),
                (byte)((val >> 16) & 0xFFUL),
                (byte)((val >> 8) & 0xFFUL),
                (byte)(val & 0xFFUL)
                );
        }
        public static (byte, byte, byte, byte, byte, byte, byte, byte) SplitLongBigEndian(long val)
        {
            return SplitULongBigEndian((ulong)val);
        }

        public static byte[] SplitArrUIntBigEndian(uint val)
        {
            var parts = SplitUIntBigEndian(val);
            return new byte[] {
                parts.Item1,
                parts.Item2,
                parts.Item3,
                parts.Item4,
            };
        }
        public static byte[] SplitArrIntBigEndian(int val)
        {
            return SplitArrUIntBigEndian((uint)val);
        }
        public static byte[] SplitArrUShortBigEndian(ushort val)
        {
            var parts = SplitUShortBigEndian(val);
            return new byte[] {
                parts.Item1,
                parts.Item2,
            };
        }
        public static byte[] SplitArrShortBigEndian(short val)
        {
            return SplitArrUShortBigEndian((ushort)val);
        }
        public static byte[] SplitArrULongBigEndian(ulong val)
        {
            var parts = SplitULongBigEndian(val);
            return new byte[] {
                parts.Item1,
                parts.Item2,
                parts.Item3,
                parts.Item4,
                parts.Item5,
                parts.Item6,
                parts.Item7,
                parts.Item8,
            };
        }
        public static byte[] SplitArrLongBigEndian(long val)
        {
            return SplitArrULongBigEndian((ulong)val);
        }

        public static uint ReverseEndianUInt(uint val)
        {
            var parts = SplitUIntBigEndian(val);
            return ComposeUIntBigEndian(parts.Item4, parts.Item3, parts.Item2, parts.Item1);
        }
        public static int ReverseEndianInt(int val)
        {
            return (int)ReverseEndianUInt((uint)val);
        }
        public static ushort ReverseEndianUShort(ushort val)
        {
            var parts = SplitUShortBigEndian(val);
            return ComposeUShortBigEndian(parts.Item2, parts.Item1);
        }
        public static short ReverseEndianShort(short val)
        {
            return (short)ReverseEndianUShort((ushort)val);
        }
        public static ulong ReverseEndianULong(ulong val)
        {
            var parts = SplitULongBigEndian(val);
            return ComposeULongBigEndian(parts.Item8, parts.Item7, parts.Item6, parts.Item5, parts.Item4, parts.Item3, parts.Item2, parts.Item1);
        }
        public static long ReverseEndianLong(long val)
        {
            return (long)ReverseEndianULong((ulong)val);
        }

        public static uint ConvertToUInt(int n)
        {
            return (uint)n;
        }
        public static int ConvertToInt(uint n)
        {
            return (int)n;
        }
        public static ushort ConvertToUShort(short n)
        {
            return (ushort)n;
        }
        public static short ConvertToShort(ushort n)
        {
            return (short)n;
        }
        public static ulong ConvertToULong(long n)
        {
            return (ulong)n;
        }
        public static long ConvertToLong(ulong n)
        {
            return (long)n;
        }
    }

    public class LuaSplitter : DataSplitter<LuaSplitter>, IBuffered
    {
        public static new DataSplitterFactory Factory // for lua
        {
            get
            {
                return DataSplitter<LuaSplitter>.Factory;
            }
        }

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        public InsertableStream ReadBuffer = new NativeBufferStream();
#else
        public InsertableStream ReadBuffer = new ArrayBufferStream();
#endif
        public Stream InputStream { get { return _InputStream; } }

        public LuaSplitter()
        {
            var l = HotFixCaller.GetLuaStateForHotFix();
            using (var lr = l.CreateStackRecover())
            {
                var tab = l.Require("capsnetlua");
                l.CallGlobal("___LuaNet__Init", Pack());
            }
        }
        public LuaSplitter(Stream input) : this()
        {
            Attach(input);
        }

        public void FireReceiveBlockBase(InsertableStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            base.FireReceiveBlock(buffer, size, type, flags, seq, sseq, exFlags);
        }
        protected override void FireReceiveBlock(InsertableStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal("___LuaNet__FireReceiveBlock", Pack(this, buffer, size, type, flags, seq, sseq, exFlags));
            base.FireReceiveBlock(buffer, size, type, flags, seq, sseq, exFlags);
        }
        public override void ReadBlock()
        {
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal("___LuaNet__ReadBlock", Pack(this));
        }

        public int BufferedSize { get { return (_BufferedStream == null ? 0 : _BufferedStream.BufferedSize); } }
        public override bool TryReadBlock()
        {
            if (_BufferedStream == null)
            {
                ReadBlock();
                return true;
            }
            else
            {
                bool result;
                var l = HotFixCaller.GetLuaStateForHotFix();
                l.CallGlobal(out result, "___LuaNet__TryReadBlock", Pack(this));
                return result;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (ReadBuffer != null)
            {
                ReadBuffer.Dispose();
                ReadBuffer = null;
            }
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal("___LuaNet__Dispose", Pack());
            base.Dispose(disposing);
        }
    }

    public class LuaComposer : DataComposer
    {
        public override void PrepareBlock(InsertableStream data, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal("___LuaNet__PrepareBlock", Pack(data, type, flags, seq, sseq, exFlags));
        }
    }

    public class LuaFormatter : DataFormatter
    {
        public class LuaFormatterFactory : DataFormatterFactory
        {
            public override DataFormatter Create(IChannel connection)
            {
                return new LuaFormatter();
            }
        }
        public static readonly LuaFormatterFactory Factory = new LuaFormatterFactory();

        public override object GetExFlags(object data)
        {
            object exflags;
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal(out exflags, "___LuaNet__GetExFlags", Pack(data));
            return exflags;
        }
        public override uint GetDataType(object data)
        {
            uint type;
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal(out type, "___LuaNet__GetDataType", Pack(data));
            return type;
        }
        public override bool CanWrite(object data)
        {
            bool result;
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal(out result, "___LuaNet__CanWrite", Pack(data));
            return result;
        }
        public override bool IsOrdered(object data)
        {
            bool result;
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal(out result, "___LuaNet__IsOrdered", Pack(data));
            return result;
        }
        public override InsertableStream Write(object data)
        {
            InsertableStream result;
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal(out result, "___LuaNet__Write", Pack(data));
            return result;
        }
        public override object Read(uint type, InsertableStream buffer, int offset, int cnt, object exFlags)
        {
            object result;
            var l = HotFixCaller.GetLuaStateForHotFix();
            l.CallGlobal(out result, "___LuaNet__Read", Pack(type, buffer, offset, cnt, exFlags));
            return result;
        }
    }
}
#endregion


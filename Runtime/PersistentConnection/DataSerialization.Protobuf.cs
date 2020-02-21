using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
using UnityEngine;
#endif
using Capstones.UnityEngineEx;
using System.IO;

using PlatDependant = Capstones.UnityEngineEx.PlatDependant;

namespace Capstones.Net
{
    //public static class ProtobufWireDataReaderAndWriter
    //{
    //    public static uint ReadFixed32(this Stream stream)
    //    {
    //        uint b1 = (uint)stream.ReadByte();
    //        uint b2 = (uint)stream.ReadByte();
    //        uint b3 = (uint)stream.ReadByte();
    //        uint b4 = (uint)stream.ReadByte();
    //        return b1 | (b2 << 8) | (b3 << 16) | (b4 << 24);
    //    }
    //    public static ulong ReadFixed64(this Stream stream)
    //    {
    //        ulong b1 = (uint)stream.ReadByte();
    //        ulong b2 = (uint)stream.ReadByte();
    //        ulong b3 = (uint)stream.ReadByte();
    //        ulong b4 = (uint)stream.ReadByte();
    //        ulong b5 = (uint)stream.ReadByte();
    //        ulong b6 = (uint)stream.ReadByte();
    //        ulong b7 = (uint)stream.ReadByte();
    //        ulong b8 = (uint)stream.ReadByte();
    //        return b1 | (b2 << 8) | (b3 << 16) | (b4 << 24)
    //               | (b5 << 32) | (b6 << 40) | (b7 << 48) | (b8 << 56);
    //    }
    //    public static uint ReadVarint32(this Stream input)
    //    {
    //        int result = 0;
    //        int offset = 0;
    //        for (; offset < 32; offset += 7)
    //        {
    //            int b = input.ReadByte();
    //            if (b == -1)
    //            {
    //                throw new FormatException("TruncatedVarint");
    //            }
    //            result |= (b & 0x7f) << offset;
    //            if ((b & 0x80) == 0)
    //            {
    //                return (uint)result;
    //            }
    //        }
    //        // Keep reading up to 64 bits.
    //        for (; offset < 64; offset += 7)
    //        {
    //            int b = input.ReadByte();
    //            if (b == -1)
    //            {
    //                throw new FormatException("TruncatedVarint");
    //            }
    //            if ((b & 0x80) == 0)
    //            {
    //                return (uint)result;
    //            }
    //        }
    //        throw new FormatException("MalformedVarint");
    //    }
    //    public static ulong ReadVarint64(this Stream input)
    //    {
    //        ulong result = 0;
    //        int offset = 0;
    //        for (; offset < 64; offset += 7)
    //        {
    //            int b = input.ReadByte();
    //            if (b == -1)
    //            {
    //                throw new FormatException("TruncatedVarint");
    //            }
    //            result |= ((ulong)(b & 0x7f)) << offset;
    //            if ((b & 0x80) == 0)
    //            {
    //                return result;
    //            }
    //        }
    //        throw new FormatException("MalformedVarint");
    //    }
    //}

    /// <summary>
    /// message Message { fixed32 type = 1; fixed32 flags = 2; fixed32 seq = 3; fixed32 sseq = 4; OtherMessage raw = 5; }
    /// </summary>
    public class ProtobufSplitter : DataSplitter, IBuffered
    {
        public static readonly DataSplitterFactory Factory = new DataSplitterFactory<ProtobufSplitter>();

        private Google.Protobuf.CodedInputStream _CodedInputStream;
        private NativeBufferStream _ReadBuffer = new NativeBufferStream();

        protected override void Attach(Stream input)
        {
            base.Attach(input);
            _CodedInputStream = new Google.Protobuf.CodedInputStream(input, true);
        }
        public ProtobufSplitter() { }
        public ProtobufSplitter(Stream input) : this()
        {
            Attach(input);
        }

        public override void ReadBlock()
        {
            while (true)
            { // Read Each Tag-Field
                if (_CodedInputStream.IsAtEnd)
                {
                    return;
                }
                if (_Type == 0)
                { // Determine the start of a message.
                    while (_Tag == 0)
                    {
                        try
                        {
                            if (_CodedInputStream.IsAtEnd)
                            {
                                return;
                            }
                            _Tag = _CodedInputStream.ReadTag();
                        }
                        catch (Google.Protobuf.InvalidProtocolBufferException e)
                        {
                            PlatDependant.LogError(e);
                        }
                        catch (InvalidOperationException)
                        {
                            // this means the stream is closed. so we ignore the exception.
                            //PlatDependant.LogError(e);
                            return;
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(e);
                            return;
                        }
                    }
                }
                else
                { // The Next tag must follow
                    try
                    {
                        _Tag = _CodedInputStream.ReadTag();
                        if (_Tag == 0)
                        {
                            ResetReadBlockContext();
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                        ResetReadBlockContext();
                        continue;
                    }
                }
                try
                { // Tag got.
                    int seq = Google.Protobuf.WireFormat.GetTagFieldNumber(_Tag);
                    var ttype = Google.Protobuf.WireFormat.GetTagWireType(_Tag);
                    if (seq == 1)
                    {
                        if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                        {
                            ResetReadBlockContext();
                            _Type = _CodedInputStream.ReadUInt32();
                        }
                        else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                        {
                            ResetReadBlockContext();
                            _Type = _CodedInputStream.ReadFixed32();
                        }
                    }
                    else if (_Type != 0)
                    {
                        if (seq == 2)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                _Flags = _CodedInputStream.ReadUInt32();
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                _Flags = _CodedInputStream.ReadFixed32();
                            }
                        }
                        else if (seq == 3)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                _Seq = _CodedInputStream.ReadUInt32();
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                _Seq = _CodedInputStream.ReadFixed32();
                            }
                        }
                        else if (seq == 4)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                _SSeq = _CodedInputStream.ReadUInt32();
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                _SSeq = _CodedInputStream.ReadFixed32();
                            }
                        }
                        else if (seq == 5)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.LengthDelimited)
                            {
                                _Size = _CodedInputStream.ReadLength();
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                _Size = (int)_CodedInputStream.ReadFixed32();
                            }
                            else
                            {
                                _Size = 0;
                            }
                            if (_Size >= 0)
                            {
                                if (_Size > CONST.MAX_MESSAGE_LENGTH)
                                {
                                    PlatDependant.LogError("We got a too long message. We will drop this message and treat it as an error message.");
                                    _CodedInputStream.SkipRawBytes(_Size);
                                    FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq);
                                }
                                else
                                {
                                    _ReadBuffer.Clear();
                                    _CodedInputStream.ReadRawBytes(_ReadBuffer, _Size);
                                    FireReceiveBlock(_ReadBuffer, _Size, _Type, _Flags, _Seq, _SSeq);
                                }
                            }
                            else
                            {
                                FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq);
                            }
                            ResetReadBlockContext();
                            return;
                        }
                    }
                    // else means the first field(type) has not been read yet.
                    _Tag = 0;
                }
                catch (InvalidOperationException)
                {
                    // this means the stream is closed. so we ignore the exception.
                    //PlatDependant.LogError(e);
                    return;
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                    ResetReadBlockContext();
                }
            }
        }

        private enum ParsingVariant
        {
            Nothing = 0,
            Tag,
            Type,
            Flags,
            Seq,
            SSeq,
            Size,
            Unknown,
            UnknownSize,
            Content,
            UnknownContent,
        }
        private uint _Tag = 0;
        private uint _Type = 0;
        private uint _Flags = 0;
        private uint _Seq = 0;
        private uint _SSeq = 0;
        private int _Size = 0;
        private ParsingVariant _ParsingVariant = 0;
        private int _ParsingVariantIndex = 0;
        private byte[] _ParsingVariantData = new byte[5];
        private void ResetReadBlockContext()
        {
            _Tag = 0;
            _Type = 0;
            _Flags = 0;
            _Seq = 0;
            _SSeq = 0;
            _Size = 0;
            _ParsingVariant = 0;
            _ParsingVariantIndex = 0;
        }
        public int BufferedSize { get { return (_BufferedStream == null ? 0 : _BufferedStream.BufferedSize) + _CodedInputStream.BufferedSize; } }
        public override bool TryReadBlock()
        {
            if (_BufferedStream == null)
            {
                ReadBlock();
                return true;
            }
            else
            {
                try
                {
                    while (true)
                    {
                        while (_ParsingVariant != 0)
                        {
                            if (BufferedSize < 1)
                            {
                                return false;
                            }
                            else
                            {
                                if (_ParsingVariant == ParsingVariant.Content)
                                {
                                    if (_ParsingVariantIndex == -1)
                                    { // read content
                                        if (BufferedSize < _Size)
                                        {
                                            return false;
                                        }
                                        else
                                        {
                                            _ReadBuffer.Clear();
                                            _CodedInputStream.ReadRawBytes(_ReadBuffer, _Size);
                                            FireReceiveBlock(_ReadBuffer, _Size, _Type, _Flags, _Seq, _SSeq);
                                        }
                                    }
                                    else
                                    { // skip content
                                        var bufferedSize = BufferedSize;
                                        if (_ParsingVariantIndex + bufferedSize < _Size)
                                        { // not enough
                                            _CodedInputStream.SkipRawBytes(bufferedSize);
                                            _ParsingVariantIndex += bufferedSize;
                                            return false;
                                        }
                                        else
                                        {
                                            var skipsize = _Size - _ParsingVariantIndex;
                                            _CodedInputStream.SkipRawBytes(skipsize);
                                            PlatDependant.LogError("We got a too long message. We will drop this message and treat it as an error message.");
                                            FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq);
                                        }
                                    }
                                    ResetReadBlockContext();
                                    return true;
                                }
                                else if (_ParsingVariant == ParsingVariant.Unknown)
                                {
                                    if (_ParsingVariantIndex == -1)
                                    {
                                        if (BufferedSize < 4)
                                        {
                                            return false;
                                        }
                                        _CodedInputStream.ReadFixed32();
                                        _ParsingVariant = 0;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else if (_ParsingVariantIndex == -2)
                                    {
                                        if (BufferedSize < 8)
                                        {
                                            return false;
                                        }
                                        _CodedInputStream.ReadFixed64();
                                        _ParsingVariant = 0;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else
                                    {
                                        while (_ParsingVariant != 0 && BufferedSize > 0)
                                        {
                                            _CodedInputStream.ReadRawBytes(_ParsingVariantData, 1);
                                            var data = _ParsingVariantData[0];
                                            if (data < 128)
                                            {
                                                _ParsingVariant = 0;
                                                _ParsingVariantIndex = 0;
                                            }
                                        }
                                        if (_ParsingVariant != 0)
                                        {
                                            return false;
                                        }
                                    }
                                }
                                else if (_ParsingVariant == ParsingVariant.UnknownSize)
                                {
                                    while (true)
                                    {
                                        if (BufferedSize <= 0)
                                        {
                                            return false;
                                        }
                                        _CodedInputStream.ReadRawBytes(_ParsingVariantData, 1);
                                        var data = _ParsingVariantData[0];
                                        uint partVal;
                                        if (data >= 128)
                                        {
                                            partVal = (uint)data - 128;
                                        }
                                        else
                                        {
                                            partVal = (uint)data;
                                        }
                                        partVal <<= 7 * _ParsingVariantIndex++;
                                        _Size += (int)partVal;
                                        if (data < 128)
                                        {
                                            break;
                                        }
                                    }
                                    if (_Size <= 0)
                                    {
                                        ResetReadBlockContext();
                                    }
                                    else
                                    {
                                        _ParsingVariant = ParsingVariant.UnknownContent;
                                        _ParsingVariantIndex = 0;
                                    }
                                }
                                else if (_ParsingVariant == ParsingVariant.UnknownContent)
                                {
                                    var bufferedSize = BufferedSize;
                                    if (_ParsingVariantIndex + bufferedSize < _Size)
                                    { // not enough
                                        _CodedInputStream.SkipRawBytes(bufferedSize);
                                        _ParsingVariantIndex += bufferedSize;
                                        return false;
                                    }
                                    else
                                    {
                                        var skipsize = _Size - _ParsingVariantIndex;
                                        _CodedInputStream.SkipRawBytes(skipsize);
                                        _Size = 0;
                                        _ParsingVariant = 0;
                                        _ParsingVariantIndex = 0;
                                    }
                                }
                                else
                                {
                                    if (_ParsingVariantIndex == -1 || _ParsingVariantIndex == -2)
                                    {
                                        if (_ParsingVariantIndex == -1 && BufferedSize < 4 || _ParsingVariantIndex == -2 && BufferedSize < 8)
                                        {
                                            return false;
                                        }
                                        uint data = 0;
                                        if (_ParsingVariantIndex == -1)
                                        {
                                            data = _CodedInputStream.ReadFixed32();
                                        }
                                        else if (_ParsingVariantIndex == -2)
                                        {
                                            data = (uint)_CodedInputStream.ReadFixed64();
                                        }
                                        switch (_ParsingVariant)
                                        {
                                            case ParsingVariant.Tag:
                                                _Tag = data;
                                                break;
                                            case ParsingVariant.Type:
                                                _Type = data;
                                                break;
                                            case ParsingVariant.Flags:
                                                _Flags = data;
                                                break;
                                            case ParsingVariant.Seq:
                                                _Seq = data;
                                                break;
                                            case ParsingVariant.SSeq:
                                                _SSeq = data;
                                                break;
                                            case ParsingVariant.Size:
                                                _Size = (int)data;
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        while (true)
                                        {
                                            if (BufferedSize <= 0)
                                            {
                                                return false;
                                            }
                                            _CodedInputStream.ReadRawBytes(_ParsingVariantData, 1);
                                            var data = _ParsingVariantData[0];
                                            uint partVal;
                                            if (data >= 128)
                                            {
                                                partVal = (uint)data - 128;
                                            }
                                            else
                                            {
                                                partVal = (uint)data;
                                            }
                                            partVal <<= 7 * _ParsingVariantIndex++;
                                            switch (_ParsingVariant)
                                            {
                                                case ParsingVariant.Tag:
                                                    _Tag += partVal;
                                                    break;
                                                case ParsingVariant.Type:
                                                    _Type += partVal;
                                                    break;
                                                case ParsingVariant.Flags:
                                                    _Flags += partVal;
                                                    break;
                                                case ParsingVariant.Seq:
                                                    _Seq += partVal;
                                                    break;
                                                case ParsingVariant.SSeq:
                                                    _SSeq += partVal;
                                                    break;
                                                case ParsingVariant.Size:
                                                    _Size += (int)partVal;
                                                    break;
                                            }
                                            if (data < 128)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    if (_ParsingVariant == ParsingVariant.Size)
                                    {
                                        if (_Size < 0)
                                        {
                                            FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq);
                                            ResetReadBlockContext();
                                            return true;
                                        }
                                        else if (_Size == 0)
                                        {
                                            FireReceiveBlock(_ReadBuffer, 0, _Type, _Flags, _Seq, _SSeq);
                                            ResetReadBlockContext();
                                            return true;
                                        }
                                        else if (_Size > CONST.MAX_MESSAGE_LENGTH)
                                        {
                                            _ParsingVariant = ParsingVariant.Content;
                                            _ParsingVariantIndex = 0;
                                        }
                                        else
                                        {
                                            _ParsingVariant = ParsingVariant.Content;
                                            _ParsingVariantIndex = -1;
                                        }
                                    }
                                    else
                                    {
                                        _ParsingVariant = 0;
                                        _ParsingVariantIndex = 0;
                                    }
                                }
                            }
                        }

                        if (_Tag == 0)
                        {
                            _ParsingVariant = ParsingVariant.Tag;
                            _ParsingVariantIndex = 0;
                        }
                        else
                        {
                            int seq = Google.Protobuf.WireFormat.GetTagFieldNumber(_Tag);
                            var ttype = Google.Protobuf.WireFormat.GetTagWireType(_Tag);
                            if (seq <= 0 || seq > 15 ||
                                ttype != Google.Protobuf.WireFormat.WireType.Varint
                                    && ttype != Google.Protobuf.WireFormat.WireType.LengthDelimited
                                    && ttype != Google.Protobuf.WireFormat.WireType.Fixed32
                                    && ttype != Google.Protobuf.WireFormat.WireType.Fixed64)
                            {
                                ResetReadBlockContext(); // the seq totally incorrect. or incorrect wiretype.
                            }
                            else if (seq == 1)
                            {
                                ResetReadBlockContext();
                                if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                {
                                    _ParsingVariant = ParsingVariant.Type;
                                    _ParsingVariantIndex = 0;
                                }
                                else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                {
                                    _ParsingVariant = ParsingVariant.Type;
                                    _ParsingVariantIndex = -1;
                                }
                                else
                                {
                                    //ResetReadBlockContext(); // the type's number is too large.
                                }
                            }
                            else if (_Type != 0)
                            {
                                if (seq == 2)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                    {
                                        _ParsingVariant = ParsingVariant.Flags;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        _ParsingVariant = ParsingVariant.Flags;
                                        _ParsingVariantIndex = -1;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed64)
                                    {
                                        _ParsingVariant = ParsingVariant.Flags;
                                        _ParsingVariantIndex = -2;
                                    }
                                    else
                                    {
                                        _ParsingVariant = ParsingVariant.UnknownSize;
                                        _ParsingVariantIndex = 0;
                                    }
                                }
                                else if (seq == 3)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                    {
                                        _ParsingVariant = ParsingVariant.Seq;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        _ParsingVariant = ParsingVariant.Seq;
                                        _ParsingVariantIndex = -1;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed64)
                                    {
                                        _ParsingVariant = ParsingVariant.Seq;
                                        _ParsingVariantIndex = -2;
                                    }
                                    else
                                    {
                                        _ParsingVariant = ParsingVariant.UnknownSize;
                                        _ParsingVariantIndex = 0;
                                    }
                                }
                                else if (seq == 4)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                    {
                                        _ParsingVariant = ParsingVariant.SSeq;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        _ParsingVariant = ParsingVariant.SSeq;
                                        _ParsingVariantIndex = -1;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed64)
                                    {
                                        _ParsingVariant = ParsingVariant.SSeq;
                                        _ParsingVariantIndex = -2;
                                    }
                                    else
                                    {
                                        _ParsingVariant = ParsingVariant.UnknownSize;
                                        _ParsingVariantIndex = 0;
                                    }
                                }
                                else if (seq == 5)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint || ttype == Google.Protobuf.WireFormat.WireType.LengthDelimited)
                                    {
                                        _ParsingVariant = ParsingVariant.Size;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        _ParsingVariant = ParsingVariant.Size;
                                        _ParsingVariantIndex = -1;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed64)
                                    {
                                        _ParsingVariant = ParsingVariant.Size;
                                        _ParsingVariantIndex = -2;
                                    }
                                }
                                else
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                    {
                                        _ParsingVariant = ParsingVariant.Unknown;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        _ParsingVariant = ParsingVariant.Unknown;
                                        _ParsingVariantIndex = -1;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed64)
                                    {
                                        _ParsingVariant = ParsingVariant.Unknown;
                                        _ParsingVariantIndex = -2;
                                    }
                                    else
                                    {
                                        _ParsingVariant = ParsingVariant.UnknownSize;
                                        _ParsingVariantIndex = 0;
                                    }
                                }
                            }
                            _Tag = 0; // reset tag
                        }
                    }
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                    return false;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_ReadBuffer != null)
            {
                _ReadBuffer.Dispose();
                _ReadBuffer = null;
            }
            if (_CodedInputStream != null)
            {
                _CodedInputStream.Dispose();
                _CodedInputStream = null;
            }
            base.Dispose(disposing);
        }
    }

    //public class JsonSplitter : DataSplitter
    //{

    //}

    public class ProtobufComposer : DataComposer
    {
        private const int _CODED_STREAM_POOL_SLOT = 4;
        private static Google.Protobuf.CodedOutputStream[] _CodedOutputStreamPool = new Google.Protobuf.CodedOutputStream[_CODED_STREAM_POOL_SLOT];
        private static int _CodedOutputStreamPoolCnt = 0;

        private static Google.Protobuf.CodedOutputStream GetCodedOutputStream()
        {
            var index = System.Threading.Interlocked.Decrement(ref _CodedOutputStreamPoolCnt);
            if (index < 0)
            {
                System.Threading.Interlocked.Increment(ref _CodedOutputStreamPoolCnt);
            }
            else
            {
                SpinWait spin = new SpinWait();
                while (true)
                {
                    var old = _CodedOutputStreamPool[index];
                    if (old != null && System.Threading.Interlocked.CompareExchange(ref _CodedOutputStreamPool[index], null, old) == old)
                    {
                        return old;
                    }
                    spin.SpinOnce();
                }
            }
            return new Google.Protobuf.CodedOutputStream((Stream)null, true);
        }
        private static void ReturnCodedOutputStream(Google.Protobuf.CodedOutputStream stream)
        {
            if (stream != null)
            {
                var index = System.Threading.Interlocked.Increment(ref _CodedOutputStreamPoolCnt);
                if (index > _CODED_STREAM_POOL_SLOT)
                {
                    System.Threading.Interlocked.Decrement(ref _CodedOutputStreamPoolCnt);
                }
                else
                {
                    --index;
                    SpinWait spin = new SpinWait();
                    while (System.Threading.Interlocked.CompareExchange(ref _CodedOutputStreamPool[index], stream, null) != null) spin.SpinOnce();
                }
            }
        }

        public override void PrepareBlock(NativeBufferStream data, uint type, uint flags, uint seq, uint sseq)
        {
            if (data != null)
            {
                var size = data.Count;
                var codedstream = GetCodedOutputStream();
                codedstream.Reinit(data);
                data.InsertMode = true;
                data.Seek(0, SeekOrigin.Begin);
                codedstream.WriteTag(1, Google.Protobuf.WireFormat.WireType.Fixed32);
                codedstream.WriteFixed32(type);
                codedstream.WriteTag(2, Google.Protobuf.WireFormat.WireType.Fixed32);
                codedstream.WriteFixed32(flags);
                codedstream.WriteTag(3, Google.Protobuf.WireFormat.WireType.Fixed32);
                codedstream.WriteFixed32(seq);
                codedstream.WriteTag(4, Google.Protobuf.WireFormat.WireType.Fixed32);
                codedstream.WriteFixed32(sseq);
                codedstream.WriteTag(5, Google.Protobuf.WireFormat.WireType.LengthDelimited);
                codedstream.WriteLength(size);
                codedstream.Flush();
                ReturnCodedOutputStream(codedstream);
            }
        }
    }

    public partial class ProtobufReaderAndWriter : DataReaderAndWriter
    {
        private static Dictionary<uint, Google.Protobuf.MessageParser> _DataParsers;
        private static Dictionary<uint, Google.Protobuf.MessageParser> DataParsers
        {
            get
            {
                if (_DataParsers == null)
                {
                    _DataParsers = new Dictionary<uint, Google.Protobuf.MessageParser>();
                }
                return _DataParsers;
            }
        }
        private static Dictionary<Type, uint> _RegisteredTypes;
        private static Dictionary<Type, uint> RegisteredTypes
        {
            get
            {
                if (_RegisteredTypes == null)
                {
                    _RegisteredTypes = new Dictionary<Type, uint>();
                }
                return _RegisteredTypes;
            }
        }

        public class RegisteredType
        {
            public RegisteredType(uint id, Type messageType, Google.Protobuf.MessageParser parser)
            {
                DataParsers[id] = parser;
                RegisteredTypes[messageType] = id;
            }
        }

        public override uint GetDataType(object data)
        {
            if (data == null)
            {
                return 0;
            }
            uint rv;
            RegisteredTypes.TryGetValue(data.GetType(), out rv);
            return rv;
        }
        public override object Read(uint type, NativeBufferStream buffer, int offset, int cnt)
        {
            Google.Protobuf.MessageParser parser;
            DataParsers.TryGetValue(type, out parser);
            if (parser != null)
            {
                try
                {
                    buffer.Seek(offset, SeekOrigin.Begin);
                    buffer.SetLength(offset + cnt);
                    var rv = parser.ParseFrom(buffer);
                    return rv;
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
            }
            return null;
        }

        [ThreadStatic] protected static Google.Protobuf.CodedOutputStream _CodedStream;
        protected static Google.Protobuf.CodedOutputStream CodedStream
        {
            get
            {
                var stream = _CodedStream;
                if (stream == null)
                {
                    stream = new Google.Protobuf.CodedOutputStream(new NativeBufferStream(), true);
                    _CodedStream = stream;
                }
                return stream;
            }
        }
        public override NativeBufferStream Write(object data)
        {
            Google.Protobuf.IMessage message = data as Google.Protobuf.IMessage;
            if (message != null)
            {
                var ostream = CodedStream;
                var stream = ostream.OutputStream as NativeBufferStream;
                if (stream == null)
                {
                    stream = new NativeBufferStream();
                }
                else
                {
                    stream.Clear();
                }
                ostream.Reinit(stream);
                message.WriteTo(ostream);
                ostream.Flush();
#if DEBUG_PERSIST_CONNECT
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("Encode ");
                    sb.Append(stream.Count);
                    sb.Append(" of type ");
                    sb.Append(GetDataType(data));
                    sb.Append(" (");
                    sb.Append(data.GetType().Name);
                    sb.Append(")");
                    for (int i = 0; i < stream.Count; ++i)
                    {
                        if (i % 32 == 0)
                        {
                            sb.AppendLine();
                        }
                        sb.Append(stream[i].ToString("X2"));
                        sb.Append(" ");
                    }
                    PlatDependant.LogInfo(sb);
                    object decodeback = null;
                    try
                    {
                        decodeback = Read(GetDataType(data), stream, 0, stream.Count);
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                    }
                    if (!Equals(data, decodeback))
                    {
                        PlatDependant.LogError("Data changed when trying to decode back.");

                        var memstream = new MemoryStream();
                        var codecnew = new Google.Protobuf.CodedOutputStream(memstream);
                        message.WriteTo(codecnew);
                        codecnew.Flush();
                        var bytes = memstream.ToArray();
                        sb.Clear();
                        sb.Append("Test Encode ");
                        sb.Append(bytes.Length);
                        sb.Append(" of type ");
                        sb.Append(GetDataType(data));
                        sb.Append(" (");
                        sb.Append(data.GetType().Name);
                        sb.Append(")");
                        for (int i = 0; i < bytes.Length; ++i)
                        {
                            if (i % 32 == 0)
                            {
                                sb.AppendLine();
                            }
                            sb.Append(bytes[i].ToString("X2"));
                            sb.Append(" ");
                        }
                        PlatDependant.LogError(sb);
                        codecnew.Dispose();
                    }
                }
#endif
                return stream;
            }
            return null;
        }
    }
}

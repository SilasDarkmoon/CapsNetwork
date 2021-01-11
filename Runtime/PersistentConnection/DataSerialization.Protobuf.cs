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
    public class WrapperStream : Stream
    {
        public Stream Underlay;

        public override bool CanRead { get { return Underlay.CanRead; } }
        public override bool CanSeek { get { return Underlay.CanSeek; } }
        public override bool CanWrite { get { return Underlay.CanWrite; } }
        public override long Length { get { return Underlay.Length; } }
        public override long Position
        {
            get { return Underlay.Position; }
            set { Underlay.Position = value; }
        }
        public override void Flush()
        {
            Underlay.Flush();
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return Underlay.Read(buffer, offset, count);
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            return Underlay.Seek(offset, origin);
        }
        public override void SetLength(long value)
        {
            Underlay.SetLength(value);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            Underlay.Write(buffer, offset, count);
        }
    }

    /// <summary>
    /// message Message { fixed32 type = 1; fixed32 flags = 2; fixed32 seq = 3; fixed32 sseq = 4; OtherMessage raw = 5; }
    /// </summary>
    public class ProtobufSplitter : DataSplitter<ProtobufSplitter>, IBuffered
    {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        private InsertableStream _ReadBuffer = new NativeBufferStream();
#else
        private InsertableStream _ReadBuffer = new ArrayBufferStream();
#endif

        public ProtobufSplitter() { }
        public ProtobufSplitter(Stream input) : this()
        {
            Attach(input);
        }

        protected override void FireReceiveBlock(InsertableStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            ResetReadBlockContext();
            base.FireReceiveBlock(buffer, size, type, flags, seq, sseq, exFlags);
        }
        public override void ReadBlock()
        {
            while (true)
            { // Read Each Tag-Field
                if (_Type == 0)
                { // Determine the start of a message.
                    while (_Tag == 0)
                    {
                        try
                        {
                            ulong tag;
                            if (!ProtobufEncoder.TryReadVariant(_InputStream, out tag))
                            {
                                return;
                            }
                            _Tag = (uint)tag;
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
                        ulong tag;
                        if (!ProtobufEncoder.TryReadVariant(_InputStream, out tag))
                        {
                            return;
                        }
                        _Tag = (uint)tag;
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
                            ulong value;
                            if (!ProtobufEncoder.TryReadVariant(_InputStream, out value))
                            {
                                return;
                            }
                            _Type = (uint)ProtobufEncoder.DecodeZigZag64(value);
                        }
                        else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                        {
                            ResetReadBlockContext();
                            uint value;
                            if (!ProtobufEncoder.TryReadFixed32(_InputStream, out value))
                            {
                                return;
                            }
                            _Type = value;
                        }
                    }
                    else if (_Type != 0)
                    {
                        if (seq == 2)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                ulong value;
                                if (!ProtobufEncoder.TryReadVariant(_InputStream, out value))
                                {
                                    return;
                                }
                                _Flags = (uint)value;
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                uint value;
                                if (!ProtobufEncoder.TryReadFixed32(_InputStream, out value))
                                {
                                    return;
                                }
                                _Flags = value;
                            }
                        }
                        else if (seq == 3)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                ulong value;
                                if (!ProtobufEncoder.TryReadVariant(_InputStream, out value))
                                {
                                    return;
                                }
                                _Seq = (uint)value;
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                uint value;
                                if (!ProtobufEncoder.TryReadFixed32(_InputStream, out value))
                                {
                                    return;
                                }
                                _Seq = value;
                            }
                        }
                        else if (seq == 4)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                ulong value;
                                if (!ProtobufEncoder.TryReadVariant(_InputStream, out value))
                                {
                                    return;
                                }
                                _SSeq = (uint)value;
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                uint value;
                                if (!ProtobufEncoder.TryReadFixed32(_InputStream, out value))
                                {
                                    return;
                                }
                                _SSeq = value;
                            }
                        }
                        else if (seq == 5)
                        {
                            if (ttype == Google.Protobuf.WireFormat.WireType.LengthDelimited)
                            {
                                ulong value;
                                if (!ProtobufEncoder.TryReadVariant(_InputStream, out value))
                                {
                                    return;
                                }
                                _Size = (int)value;
                            }
                            else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                            {
                                uint value;
                                if (!ProtobufEncoder.TryReadFixed32(_InputStream, out value))
                                {
                                    return;
                                }
                                _Size = (int)value;
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
                                    ProtobufEncoder.SkipBytes(_InputStream, _Size);
                                    FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq, null);
                                }
                                else
                                {
                                    _ReadBuffer.Clear();
                                    ProtobufEncoder.CopyBytes(_InputStream, _ReadBuffer, _Size);
                                    FireReceiveBlock(_ReadBuffer, _Size, _Type, _Flags, _Seq, _SSeq, null);
                                }
                            }
                            else
                            {
                                FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq, null);
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
        //private byte[] _ParsingVariantData = new byte[5];
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
                                            ProtobufEncoder.CopyBytes(_InputStream, _ReadBuffer, _Size);
                                            FireReceiveBlock(_ReadBuffer, _Size, _Type, _Flags, _Seq, _SSeq, null);
                                        }
                                    }
                                    else
                                    { // skip content
                                        var bufferedSize = BufferedSize;
                                        if (_ParsingVariantIndex + bufferedSize < _Size)
                                        { // not enough
                                            ProtobufEncoder.SkipBytes(_InputStream, bufferedSize);
                                            _ParsingVariantIndex += bufferedSize;
                                            return false;
                                        }
                                        else
                                        {
                                            var skipsize = _Size - _ParsingVariantIndex;
                                            ProtobufEncoder.SkipBytes(_InputStream, skipsize);
                                            PlatDependant.LogError("We got a too long message. We will drop this message and treat it as an error message.");
                                            FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq, null);
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
                                        ProtobufEncoder.ReadFixed32(_InputStream);
                                        _ParsingVariant = 0;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else if (_ParsingVariantIndex == -2)
                                    {
                                        if (BufferedSize < 8)
                                        {
                                            return false;
                                        }
                                        ProtobufEncoder.ReadFixed64(_InputStream);
                                        _ParsingVariant = 0;
                                        _ParsingVariantIndex = 0;
                                    }
                                    else
                                    {
                                        while (_ParsingVariant != 0 && BufferedSize > 0)
                                        {
                                            int b;
                                            if ((b = _InputStream.ReadByte()) < 0)
                                            {
                                                return false;
                                            }
                                            var data = (byte)b;
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
                                        int b;
                                        if ((b = _InputStream.ReadByte()) < 0)
                                        {
                                            return false;
                                        }
                                        var data = (byte)b;
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
                                        ProtobufEncoder.SkipBytes(_InputStream, bufferedSize);
                                        _ParsingVariantIndex += bufferedSize;
                                        return false;
                                    }
                                    else
                                    {
                                        var skipsize = _Size - _ParsingVariantIndex;
                                        ProtobufEncoder.SkipBytes(_InputStream, skipsize);
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
                                            data = ProtobufEncoder.ReadFixed32(_InputStream);
                                        }
                                        else if (_ParsingVariantIndex == -2)
                                        {
                                            data = (uint)ProtobufEncoder.ReadFixed64(_InputStream);
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
                                            int b;
                                            if ((b = _InputStream.ReadByte()) < 0)
                                            {
                                                return false;
                                            }
                                            var data = (byte)b;
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
                                        if (_ParsingVariant == ParsingVariant.Type)
                                        {
                                            _Type = (uint)ProtobufEncoder.DecodeZigZag32(_Type);
                                        }
                                    }
                                    if (_ParsingVariant == ParsingVariant.Size)
                                    {
                                        if (_Size < 0)
                                        {
                                            FireReceiveBlock(null, 0, _Type, _Flags, _Seq, _SSeq, null);
                                            ResetReadBlockContext();
                                            return true;
                                        }
                                        else if (_Size == 0)
                                        {
                                            FireReceiveBlock(_ReadBuffer, 0, _Type, _Flags, _Seq, _SSeq, null);
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
            base.Dispose(disposing);
        }
    }

    //public class JsonSplitter : DataSplitter
    //{

    //}

    public class ProtobufComposer : DataComposer
    {
#if DEBUG_PERSIST_CONNECT || DEBUG_PERSIST_CONNECT_LOW_LEVEL
        public bool VariantHeader = true;
#else
        public bool VariantHeader = true;
#endif

        public override void PrepareBlock(InsertableStream data, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            if (data != null)
            {
                var size = data.Count;
                data.InsertMode = true;
                data.Seek(0, SeekOrigin.Begin);
                int wrotecnt = 0;
                if (VariantHeader)
                {
                    wrotecnt += ProtobufEncoder.WriteTag(1, ProtobufLowLevelType.Varint, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteVariant(ProtobufEncoder.EncodeZigZag32((int)type), data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteTag(2, ProtobufLowLevelType.Varint, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteVariant(flags, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteTag(3, ProtobufLowLevelType.Varint, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteVariant(seq, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteTag(4, ProtobufLowLevelType.Varint, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteVariant(sseq, data, wrotecnt);
                }
                else
                {
                    wrotecnt += ProtobufEncoder.WriteTag(1, ProtobufLowLevelType.Fixed32, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteFixed32(type, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteTag(2, ProtobufLowLevelType.Fixed32, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteFixed32(flags, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteTag(3, ProtobufLowLevelType.Fixed32, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteFixed32(seq, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteTag(4, ProtobufLowLevelType.Fixed32, data, wrotecnt);
                    wrotecnt += ProtobufEncoder.WriteFixed32(sseq, data, wrotecnt);
                }
                wrotecnt += ProtobufEncoder.WriteTag(5, ProtobufLowLevelType.LengthDelimited, data, wrotecnt);
                wrotecnt += ProtobufEncoder.WriteVariant((uint)size, data, wrotecnt);
            }
        }
    }

    public partial class ProtobufFormatter : DataFormatter
    {
        public override uint GetDataType(object data)
        {
            if (data == null)
            {
                return 0;
            }
            uint rv;
            if ((rv = base.GetDataType(data)) != 0)
            {
                return rv;
            }
            ProtobufReg.RegisteredTypes.TryGetValue(data.GetType(), out rv);
            return rv;
        }
        public override object Read(uint type, InsertableStream buffer, int offset, int cnt, object exFlags)
        {
            var frombase = base.Read(type, buffer, offset, cnt, exFlags);
            if (frombase != null)
            {
                return frombase;
            }
            Google.Protobuf.MessageParser parser;
            ProtobufReg.DataParsers.TryGetValue(type, out parser);
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
        [ThreadStatic] protected static InsertableStream _UnderlayStream;
        protected static Google.Protobuf.CodedOutputStream CodedStream
        {
            get
            {
                var stream = _CodedStream;
                if (stream == null)
                {
                    _CodedStream = stream =
                        new Google.Protobuf.CodedOutputStream(_UnderlayStream =
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
                        new NativeBufferStream()
#else
                        new ArrayBufferStream()
#endif
                        , true);
                }
                return stream;
            }
        }
        public override InsertableStream Write(object data)
        {
            var frombase = base.Write(data);
            if (frombase != null)
            {
                return frombase;
            }
            Google.Protobuf.IMessage message = data as Google.Protobuf.IMessage;
            if (message != null)
            {
                var ostream = CodedStream;
                _UnderlayStream.Clear();
                message.WriteTo(ostream);
                ostream.Flush();
#if DEBUG_PERSIST_CONNECT
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("Encode ");
                    sb.Append(_UnderlayStream.Count);
                    sb.Append(" of type ");
                    sb.Append(GetDataType(data));
                    sb.Append(" (");
                    sb.Append(data.GetType().Name);
                    sb.Append(")");
                    //for (int i = 0; i < _UnderlayStream.Count; ++i)
                    //{
                    //    if (i % 32 == 0)
                    //    {
                    //        sb.AppendLine();
                    //    }
                    //    sb.Append(_UnderlayStream[i].ToString("X2"));
                    //    sb.Append(" ");
                    //}
                    PlatDependant.LogInfo(sb);
                    //object decodeback = null;
                    //try
                    //{
                    //    decodeback = Read(GetDataType(data), _UnderlayStream, 0, _UnderlayStream.Count);
                    //}
                    //catch (Exception e)
                    //{
                    //    PlatDependant.LogError(e);
                    //}
                    //if (!Equals(data, decodeback))
                    //{
                    //    PlatDependant.LogError("Data changed when trying to decode back.");

                    //    var memstream = new MemoryStream();
                    //    var codecnew = new Google.Protobuf.CodedOutputStream(memstream);
                    //    message.WriteTo(codecnew);
                    //    codecnew.Flush();
                    //    var bytes = memstream.ToArray();
                    //    sb.Clear();
                    //    sb.Append("Test Encode ");
                    //    sb.Append(bytes.Length);
                    //    sb.Append(" of type ");
                    //    sb.Append(GetDataType(data));
                    //    sb.Append(" (");
                    //    sb.Append(data.GetType().Name);
                    //    sb.Append(")");
                    //    for (int i = 0; i < bytes.Length; ++i)
                    //    {
                    //        if (i % 32 == 0)
                    //        {
                    //            sb.AppendLine();
                    //        }
                    //        sb.Append(bytes[i].ToString("X2"));
                    //        sb.Append(" ");
                    //    }
                    //    PlatDependant.LogError(sb);
                    //    codecnew.Dispose();
                    //}
                }
#endif
                return _UnderlayStream;
            }
            return null;
        }
    }
}

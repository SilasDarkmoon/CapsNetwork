using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
using UnityEngine;
using Unity.Collections.Concurrent;
#endif
using Capstones.UnityEngineEx;
using System.IO;

using PlatDependant = Capstones.UnityEngineEx.PlatDependant;

namespace Capstones.Net
{
    public abstract class DataSplitter : IDisposable
    {
        public abstract void ReadBlock();
        public abstract bool TryReadBlock();

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
        }

        public void Dispose()
        {
            Dispose(true);
        }
#endregion
    }

    /// <summary>
    /// message Message { uint32 type = 1; uint32 flags = 2; uint32 seq = 3; uint32 sseq = 4; OtherMessage raw = 5; }
    /// </summary>
    public class ProtobufSplitter : DataSplitter, IBuffered
    {
        private Google.Protobuf.CodedInputStream _CodedInputStream;
        private IBuffered _BufferedStream;
        private NativeBufferStream _ReadBuffer = new NativeBufferStream();

        public ProtobufSplitter(Stream inputstream)
        {
            _CodedInputStream = new Google.Protobuf.CodedInputStream(inputstream, true);
            _BufferedStream = inputstream as IBuffered;
        }

        private uint _Tag = 0;
        private uint _Type = 0;
        private uint _Flags = 0;
        private uint _Seq = 0;
        private uint _SSeq = 0;
        private int _Size = 0;
        private bool _SizeReady = false;
        private void ResetReadBlockContext()
        {
            _Tag = 0;
            _Type = 0;
            _Flags = 0;
            _Seq = 0;
            _SSeq = 0;
            _Size = 0;
            _SizeReady = false;
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
                        catch (InvalidOperationException e)
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
                    if (seq == 1 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                    {
                        ResetReadBlockContext();
                        _Type = _CodedInputStream.ReadUInt32();
                    }
                    else if (_Type != 0)
                    {
                        if (seq == 2 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                        {
                            _Flags = _CodedInputStream.ReadUInt32();
                        }
                        else if (seq == 3 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                        {
                            _Seq = _CodedInputStream.ReadUInt32();
                        }
                        else if (seq == 4 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                        {
                            _SSeq = _CodedInputStream.ReadUInt32();
                        }
                        else if (seq == 5 && ttype == Google.Protobuf.WireFormat.WireType.LengthDelimited)
                        {
                            _Size = _CodedInputStream.ReadLength();
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
                catch (InvalidOperationException e)
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
        public int BufferedSize { get { return (_BufferedStream == null ? 0 : _BufferedStream.BufferedSize) + _CodedInputStream.BufferedSize; } }
        public override bool TryReadBlock() // TODO: BufferedSize < 1 -> actual size... may be we need to peek(n) or get and put back.
        {
            if (_BufferedStream == null)
            {
                ReadBlock();
                return true;
            }
            else
            {
                while (true)
                {
                    if (_Type == 0)
                    { // Determine the start of a message.
                        while (_Tag == 0)
                        {
                            if (BufferedSize < 1)
                            {
                                return false;
                            }
                            try
                            {
                                _Tag = _CodedInputStream.ReadTag();
                            }
                            catch (Exception e)
                            {
                                PlatDependant.LogError(e);
                            }
                        }
                    }
                    else
                    { // The Next tag must follow
                        if (_Tag == 0)
                        {
                            if (BufferedSize < 1)
                            {
                                return false;
                            }
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
                    }
                    try
                    {
                        int seq = Google.Protobuf.WireFormat.GetTagFieldNumber(_Tag);
                        var ttype = Google.Protobuf.WireFormat.GetTagWireType(_Tag);
                        if (seq == 1 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                        {
                            if (BufferedSize < 1)
                            {
                                return false;
                            }
                            ResetReadBlockContext();
                            _Type = _CodedInputStream.ReadUInt32();
                        }
                        else if (_Type != 0)
                        {
                            if (seq == 2 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                if (BufferedSize < 1)
                                {
                                    return false;
                                }
                                _Flags = _CodedInputStream.ReadUInt32();
                            }
                            else if (seq == 3 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                if (BufferedSize < 1)
                                {
                                    return false;
                                }
                                _Seq = _CodedInputStream.ReadUInt32();
                            }
                            else if (seq == 4 && ttype == Google.Protobuf.WireFormat.WireType.Varint)
                            {
                                if (BufferedSize < 1)
                                {
                                    return false;
                                }
                                _SSeq = _CodedInputStream.ReadUInt32();
                            }
                            else if (seq == 5 && ttype == Google.Protobuf.WireFormat.WireType.LengthDelimited)
                            {
                                if (!_SizeReady)
                                {
                                    if (BufferedSize < 1)
                                    {
                                        return false;
                                    }
                                    _Size = _CodedInputStream.ReadLength();
                                    _SizeReady = true;
                                }
                                if (_Size >= 0)
                                {
                                    if (BufferedSize < _Size)
                                    {
                                        return false;
                                    }
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
                                return true;
                            }
                        }
                        _Tag = 0;
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                        ResetReadBlockContext();
                    }
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
        }
    }

    //public class JsonSplitter : DataSplitter
    //{

    //}

    public abstract class DataComposer
    {
        public abstract void PrepareBlock(NativeBufferStream data, uint type, uint flags, uint seq, uint sseq);
    }

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
                codedstream.WriteTag(1, Google.Protobuf.WireFormat.WireType.Varint);
                codedstream.WriteUInt32(type);
                codedstream.WriteTag(2, Google.Protobuf.WireFormat.WireType.Varint);
                codedstream.WriteUInt32(flags);
                codedstream.WriteTag(3, Google.Protobuf.WireFormat.WireType.Varint);
                codedstream.WriteUInt32(seq);
                codedstream.WriteTag(4, Google.Protobuf.WireFormat.WireType.Varint);
                codedstream.WriteUInt32(sseq);
                codedstream.WriteTag(5, Google.Protobuf.WireFormat.WireType.LengthDelimited);
                codedstream.WriteLength(size);
                codedstream.Flush();
                ReturnCodedOutputStream(codedstream);
            }
        }
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
                return stream;
            }
            return null;
        }
    }
}

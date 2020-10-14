﻿using System;
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
    public class CarbonExFlags
    {
        public short Flags;
        public short Cate;
        public int Type;
    }

    public class CarbonMessage
    {
        public short Flags;
        public short Cate;
        public int Type;

        private object _Message;
        public object ObjMessage
        {
            get { return _Message; }
            set { _Message = value; }
        }
        public string StrMessage
        {
            get { return _Message as string; }
            set { _Message = value; }
        }
        public byte[] BytesMessage
        {
            get { return _Message as byte[]; }
            set { _Message = value; }
        }

        public bool GetFlag(int index)
        {
            return (Flags & (0x1) << index) != 0;
        }
        public void SetFlag(int index, bool value)
        {
            if (value)
            {
                Flags |= unchecked((short)(0x1 << index));
            }
            else
            {
                Flags &= unchecked((short)~(0x1 << index));
            }
        }
        public bool Encrypted
        {
            get { return GetFlag(0); }
            set { SetFlag(0, value); }
        }
        public bool ShouldPingback
        {
            get { return GetFlag(1); }
            set { SetFlag(1, value); }
        }
    }

    /// <summary>
    /// Bytes: (4 size)
    /// 2 flags, 2 proto-category, 4 message-type
    /// </summary>
    public class CarbonSplitter : DataSplitter, IBuffered
    {
        public static readonly DataSplitterFactory Factory = new DataSplitterFactory<CarbonSplitter>();

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        private InsertableStream _ReadBuffer = new NativeBufferStream();
#else
        private InsertableStream _ReadBuffer = new ArrayBufferStream();
#endif

        public CarbonSplitter() { }
        public CarbonSplitter(Stream input) : this()
        {
            Attach(input);
        }

        protected void FireReceiveBlock(InsertableStream buffer, int size, CarbonExFlags exFlags)
        {
            if (buffer == null)
            {
                FireReceiveBlock(null, 0, (uint)exFlags.Type, (uint)(ushort)exFlags.Flags, 0, 0, exFlags);
            }
            else if (exFlags.Cate == 2 && exFlags.Type == 1)
            {
                bool decodeSuccess = false;
                uint seq_client = 0;
                uint sseq = 0;
                uint pbtype = 0;
                uint pbtag = 0;
                uint pbflags = 0;
                int pbsize = 0;
                try
                {
                    buffer.Seek(0, SeekOrigin.Begin);
                    while (true)
                    { // Read Each Tag-Field
                        if (pbtype == 0)
                        { // Determine the start of a message.
                            while (pbtag == 0)
                            {
                                try
                                {
                                    ulong tag = ProtobufEncoder.ReadVariant(buffer);
                                    pbtag = (uint)tag;
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
                                ulong tag = ProtobufEncoder.ReadVariant(buffer);
                                pbtag = (uint)tag;
                                if (pbtag == 0)
                                {
                                    return;
                                }
                            }
                            catch (Exception e)
                            {
                                PlatDependant.LogError(e);
                                return;
                            }
                        }
                        try
                        { // Tag got.
                            int seq = Google.Protobuf.WireFormat.GetTagFieldNumber(pbtag);
                            var ttype = Google.Protobuf.WireFormat.GetTagWireType(pbtag);
                            if (seq == 1)
                            {
                                if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                {
                                    ulong value = ProtobufEncoder.ReadVariant(buffer);
                                    pbtype = (uint)ProtobufEncoder.DecodeZigZag64(value);
                                }
                                else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                {
                                    uint value = ProtobufEncoder.ReadFixed32(buffer);
                                    pbtype = value;
                                }
                            }
                            else if (pbtype != 0)
                            {
                                if (seq == 2)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                    {
                                        ulong value = ProtobufEncoder.ReadVariant(buffer);
                                        pbflags = (uint)value;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        uint value = ProtobufEncoder.ReadFixed32(buffer);
                                        pbflags = value;
                                    }
                                }
                                else if (seq == 3)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                    {
                                        ulong value = ProtobufEncoder.ReadVariant(buffer);
                                        seq_client = (uint)value;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        uint value = ProtobufEncoder.ReadFixed32(buffer);
                                        seq_client = value;
                                    }
                                }
                                else if (seq == 4)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.Varint)
                                    {
                                        ulong value = ProtobufEncoder.ReadVariant(buffer);
                                        sseq = (uint)value;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        uint value = ProtobufEncoder.ReadFixed32(buffer);
                                        sseq = value;
                                    }
                                }
                                else if (seq == 5)
                                {
                                    if (ttype == Google.Protobuf.WireFormat.WireType.LengthDelimited)
                                    {
                                        ulong value = ProtobufEncoder.ReadVariant(buffer);
                                        pbsize = (int)value;
                                    }
                                    else if (ttype == Google.Protobuf.WireFormat.WireType.Fixed32)
                                    {
                                        uint value = ProtobufEncoder.ReadFixed32(buffer);
                                        pbsize = (int)value;
                                    }
                                    else
                                    {
                                        pbsize = 0;
                                    }
                                    if (pbsize >= 0)
                                    {
                                        if (pbsize > buffer.Length - buffer.Position)
                                        {
                                            PlatDependant.LogError("We got a too long message. We will drop this message and treat it as an error message.");
                                            return;
                                        }
                                        else
                                        {
                                            buffer.Consume();
                                            FireReceiveBlock(buffer, pbsize, pbtype, pbflags, seq_client, sseq, exFlags);
                                            decodeSuccess = true;
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                            }
                            // else means the first field(type) has not been read yet.
                            pbtag = 0;
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
                        }
                    }
                }
                finally
                {
                    if (!decodeSuccess)
                    {
                        FireReceiveBlock(null, 0, pbtype, pbflags, seq_client, sseq, exFlags);
                    }
                }
            }
            else
            {
                FireReceiveBlock(buffer, size, (uint)exFlags.Type, (uint)(ushort)exFlags.Flags, 0, 0, exFlags);
            }
        }
        protected override void FireReceiveBlock(InsertableStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            ResetReadBlockContext();
            base.FireReceiveBlock(buffer, size, type, flags, seq, sseq, exFlags);
        }
        protected void ReadHeaders(out uint size, out short flags, out short cate, out int type)
        {
            // Read size.(4 bytes)
            size = 0;
            for (int i = 0; i < 4; ++i)
            {
                size <<= 8;
                size += (byte)_InputStream.ReadByte();
            }
            // Read flags.(2 byte)
            flags = 0;
            for (int i = 0; i < 2; ++i)
            {
                flags <<= 8;
                flags += (byte)_InputStream.ReadByte();
            }
            // Read Cate.(2 byte)
            cate = 0;
            for (int i = 0; i < 2; ++i)
            {
                cate <<= 8;
                cate += (byte)_InputStream.ReadByte();
            }
            // Read Type. (4 byte)
            type = 0;
            for (int i = 0; i < 4; ++i)
            {
                type <<= 8;
                type += (byte)_InputStream.ReadByte();
            }
        }
        public override void ReadBlock()
        {
            while (true)
            {
                try
                {
                    uint size;
                    short flags;
                    short cate;
                    int type;
                    ReadHeaders(out size, out flags, out cate, out type);
                    int realsize = (int)(size - 8);
                    var exFlags = new CarbonExFlags()
                    {
                        Flags = flags,
                        Cate = cate,
                        Type = type,
                    };
                    if (realsize >= 0)
                    {
                        if (realsize > CONST.MAX_MESSAGE_LENGTH)
                        {
                            PlatDependant.LogError("We got a too long message. We will drop this message and treat it as an error message.");
                            ProtobufEncoder.SkipBytes(_InputStream, realsize);
                            FireReceiveBlock(null, 0, exFlags);
                        }
                        else
                        {
                            _ReadBuffer.Clear();
                            ProtobufEncoder.CopyBytes(_InputStream, _ReadBuffer, realsize);
                            FireReceiveBlock(_ReadBuffer, realsize, exFlags);
                        }
                    }
                    else
                    {
                        FireReceiveBlock(null, 0, exFlags);
                    }
                    ResetReadBlockContext();
                    return;
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

        private CarbonExFlags _ExFlags;
        private int _Size;
        private void ResetReadBlockContext()
        {
            _Size = 0;
            _ExFlags = null;
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
                        if (_ExFlags == null)
                        {
                            if (BufferedSize < 12)
                            {
                                return false;
                            }
                            uint size;
                            short flags;
                            short cate;
                            int type;
                            ReadHeaders(out size, out flags, out cate, out type);
                            _ExFlags = new CarbonExFlags()
                            {
                                Flags = flags,
                                Cate = cate,
                                Type = type,
                            };
                            _Size = (int)(size - 8);
                        }
                        else
                        {
                            if (_Size >= 0)
                            {
                                if (BufferedSize < _Size)
                                {
                                    return false;
                                }
                                if (_Size > CONST.MAX_MESSAGE_LENGTH)
                                {
                                    PlatDependant.LogError("We got a too long message. We will drop this message and treat it as an error message.");
                                    ProtobufEncoder.SkipBytes(_InputStream, _Size);
                                    FireReceiveBlock(null, 0, _ExFlags);
                                }
                                else
                                {
                                    _ReadBuffer.Clear();
                                    ProtobufEncoder.CopyBytes(_InputStream, _ReadBuffer, _Size);
                                    FireReceiveBlock(_ReadBuffer, _Size, _ExFlags);
                                }
                            }
                            else
                            {
                                FireReceiveBlock(null, 0, _ExFlags);
                            }
                            ResetReadBlockContext();
                            return true;
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

    public class CarbonComposer : ProtobufComposer
    {
        public override void PrepareBlock(InsertableStream data, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            if (data != null)
            {
                CarbonExFlags carbonflags = exFlags as CarbonExFlags;
                if (carbonflags != null && carbonflags.Cate == 2 && carbonflags.Type == 1)
                {
                    // Wrapped-Protobuf
                    base.PrepareBlock(data, type, flags, seq, sseq, exFlags);
                }

                var size = data.Count;
                data.InsertMode = true;
                data.Seek(0, SeekOrigin.Begin);

                short carbonFlags = 0;
                short carbonCate = 0;
                int carbonType = 0;
                if (carbonflags != null)
                {
                    carbonFlags = carbonflags.Flags;
                    carbonCate = carbonflags.Cate;
                    carbonType = carbonflags.Type;
                }

                uint full_size = (uint)(size + 8);

                // write size.(4 bytes) (not included in full_size)
                data.WriteByte((byte)((full_size & (0xFF << 24)) >> 24));
                data.WriteByte((byte)((full_size & (0xFF << 16)) >> 16));
                data.WriteByte((byte)((full_size & (0xFF << 8)) >> 8));
                data.WriteByte((byte)(full_size & 0xFF));

                // write flags.(2 byte)
                data.WriteByte((byte)((carbonFlags & (0xFF << 8)) >> 8));
                data.WriteByte((byte)(carbonFlags & 0xFF));
                // Write Cate.(2 byte)
                data.WriteByte((byte)((carbonCate & (0xFF << 8)) >> 8));
                data.WriteByte((byte)(carbonCate & 0xFF));
                // Write Type.(4 bytes)
                data.WriteByte((byte)((carbonType & (0xFF << 24)) >> 24));
                data.WriteByte((byte)((carbonType & (0xFF << 16)) >> 16));
                data.WriteByte((byte)((carbonType & (0xFF << 8)) >> 8));
                data.WriteByte((byte)(carbonType & 0xFF));
            }
        }
    }

    public class CarbonReaderAndWriter : ProtobufReaderAndWriter
    {
        public override object GetExFlags(object data)
        {
            CarbonMessage carbonmess = data as CarbonMessage;
            if (carbonmess != null)
            {
                return new CarbonExFlags()
                {
                    Flags = carbonmess.Flags,
                    Cate = carbonmess.Cate,
                    Type = carbonmess.Type,
                };
            }
            else if (data is byte[])
            {
                return new CarbonExFlags()
                {
                    Flags = 0,
                    Cate = 2,
                    Type = -128,
                };
            }
            else if (data is string)
            {
                return new CarbonExFlags()
                {
                    Flags = 0,
                    Cate = 1,
                    Type = -128,
                };
            }
            else if (GetDataType(data) != 0)
            {
                return new CarbonExFlags()
                {
                    Flags = 0,
                    Cate = 2,
                    Type = 1,
                };
            }
            else if (data is Google.Protobuf.IMessage)
            {
                return new CarbonExFlags()
                {
                    Flags = 0,
                    Cate = 4,
                    Type = -128,
                };
            }
            else
            {
                return new CarbonExFlags()
                {
                    Flags = 0,
                    Cate = 2,
                    Type = -128,
                };
            }
        }
        public override uint GetDataType(object data)
        {
            CarbonMessage carbonmess = data as CarbonMessage;
            if (carbonmess != null)
            {
                if (carbonmess.Cate == 2 && carbonmess.Type == 1)
                {
                    return base.GetDataType(carbonmess.ObjMessage);
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return base.GetDataType(data);
            }
        }
        public override InsertableStream Write(object data)
        {
            CarbonMessage carbonmess = data as CarbonMessage;
            if (carbonmess != null)
            {
                if (carbonmess.BytesMessage != null)
                {
                    return base.Write(carbonmess.BytesMessage);
                }
                else if (carbonmess.StrMessage != null)
                {
                    return base.Write(carbonmess.StrMessage);
                }
                else if (carbonmess.ObjMessage != null)
                {
                    return base.Write(carbonmess.ObjMessage);
                }
                else
                {
                    return base.Write(PredefinedMessages.Empty);
                }
            }
            else
            {
                return base.Write(data);
            }
        }
        public override object Read(uint type, InsertableStream buffer, int offset, int cnt, object exFlags)
        {
            var carbonFlags = exFlags as CarbonExFlags;
            if (carbonFlags == null)
            {
                return base.Read(type, buffer, offset, cnt, exFlags);
            }
            else
            {
                var message = new CarbonMessage()
                {
                    Flags = carbonFlags.Flags,
                    Cate = carbonFlags.Cate,
                    Type = carbonFlags.Type,
                };
                if (carbonFlags.Cate == 3 || carbonFlags.Cate == 1)
                { // Json
                    byte[] raw = PredefinedMessages.GetRawBuffer(cnt);
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
                    message.StrMessage = str;
                }
                else if (carbonFlags.Cate == 4)
                { // PB
                    message.ObjMessage = ProtobufEncoder.ReadRaw(new ListSegment<byte>(buffer, offset, cnt));
                }
                else if (carbonFlags.Cate == 2 && carbonFlags.Type == 1)
                {
                    message.ObjMessage = base.Read(type, buffer, offset, cnt, exFlags);
                    return message.ObjMessage; // Notice: in this condition, we should not return the wrapper. Only the ObjMessage in the wrapper is meaningful.
                }
                else
                { // Raw
                    byte[] raw = new byte[cnt];
                    buffer.Seek(offset, SeekOrigin.Begin);
                    buffer.Read(raw, 0, cnt);
                    message.BytesMessage = raw;
                }
                return message;
            }
        }
    }

    public static class CarbonMessageUtils
    {
        public class Heartbeat : IDisposable
        {
            protected IReqClient _Client;
            public object _HeartbeatObj;
            public Func<object> _HeartbeatCreator;

            protected int _LastTick;
            public int LastTick { get { return _LastTick; } }
            public int Interval = 1000;
            public int Timeout = -1;
            protected bool _Dead = false;
            public bool Dead { get { return _Dead; } }

            public event Action OnDead = () => { };
            protected void OnHeartbeatDead()
            {
                Dispose();
                _Dead = true;
                var disposable = _Client as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
                OnDead();
            }

            public Heartbeat(IReqClient client)
            {
                _Client = client;
                Start();
            }
            public Heartbeat(IReqClient client, object heartbeatObj)
                : this(client)
            {
                _HeartbeatObj = heartbeatObj;
            }
            public Heartbeat(IReqClient client, Func<object> heartbeatCreator)
                : this(client)
            {
                _HeartbeatCreator = heartbeatCreator;
            }

            public void Start()
            {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
                if (ThreadSafeValues.IsMainThread)
                {
                    CoroutineRunner.StartCoroutine(SendHeartbeatWork());
                }
                else
#endif
                {
                    PlatDependant.RunBackgroundLongTime(prog =>
                    {
                        try
                        {
                            _LastTick = Environment.TickCount;
                            while (_Client.IsAlive && !_Disposed)
                            {
                                if (_Client.IsStarted)
                                {
                                    object heartbeat = null;
                                    if (_HeartbeatCreator != null)
                                    {
                                        heartbeat = _HeartbeatCreator();
                                    }
                                    if (heartbeat == null)
                                    {
                                        heartbeat = _HeartbeatObj;
                                    }
                                    if (heartbeat == null)
                                    {
                                        heartbeat = PredefinedMessages.Empty;
                                    }
                                    SendHeartbeatAsync(heartbeat);
                                }
                                else
                                {
                                    _LastTick = Environment.TickCount;
                                }
                                var interval = Interval;
                                if (interval < 0)
                                {
                                    interval = 1000;
                                }
                                Thread.Sleep(interval);
                                if (Timeout > 0 && Environment.TickCount > _LastTick + Timeout)
                                {
                                    break;
                                }
                            }
                        }
                        finally
                        {
                            if (_Client.IsAlive && !_Disposed)
                            {
                                OnHeartbeatDead();
                            }
                        }
                    });
                }
            }

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
            protected IEnumerator SendHeartbeatWork()
            {
                try
                {
                    _LastTick = Environment.TickCount;
                    while (_Client != null && _Client.IsAlive && !_Disposed)
                    {
                        if (_Client.IsStarted)
                        {
                            object heartbeat = null;
                            if (_HeartbeatCreator != null)
                            {
                                heartbeat = _HeartbeatCreator();
                            }
                            if (heartbeat == null)
                            {
                                heartbeat = _HeartbeatObj;
                            }
                            if (heartbeat == null)
                            {
                                heartbeat = PredefinedMessages.Empty;
                            }
                            SendHeartbeatAsync(heartbeat);
                        }
                        else
                        {
                            _LastTick = Environment.TickCount;
                        }
                        var interval = Interval;
                        if (interval < 0)
                        {
                            interval = 1000;
                        }
                        yield return new WaitForSecondsRealtime(interval / 1000f);
                        if (Timeout > 0 && Environment.TickCount - _LastTick > Timeout)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    if (_Client != null && _Client.IsAlive && !_Disposed)
                    {
                        OnHeartbeatDead();
                    }
                }
            }
#endif

            protected async void SendHeartbeatAsync(object heartbeat)
            {
                try
                {
                    if (Timeout > 0)
                    {
                        var request = _Client.Send(heartbeat, 10000);
                        await request;
                        if (request.Error != null)
                        {
                            PlatDependant.LogError(request.Error);
                        }
                    }
                    else
                    {
                        _Client.SendMessage(heartbeat);
                    }
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
            }

            protected bool _Disposed;
            public void Dispose()
            {
                if (!_Disposed)
                {
                    _Disposed = true;
                }
            }
        }
        public static readonly CarbonMessage HeartbeatMessage = new CarbonMessage();

        public class CarbonMessageHandler : IDisposable
        {
            public Action OnClose;
            public Action<short, int, object> OnMessage;
            public Action<string, int> OnJsonMessage;

            public CarbonMessageHandler(IReqClient client)
            {
                var handler = client as ReqHandler;
                if (handler != null)
                {
                    Action onClose = null;
                    onClose = () =>
                    {
                        Dispose();
                        handler.OnClose -= onClose;
                        handler.RemoveHandler(MessageHandler);
                    };
                    handler.OnClose += onClose;
                    handler.RegHandler(MessageHandler);
                }
            }

            [EventOrder(100)]
            public object MessageHandler(IReqClient from, uint type, object reqobj, uint seq)
            {
                var carbon = reqobj as CarbonMessage;
                if (carbon != null)
                {
                    if (carbon.Cate == 3)
                    {
                        if (OnJsonMessage != null)
                        {
                            OnJsonMessage(carbon.StrMessage, carbon.Type);
                        }
                    }
                    if (OnMessage != null)
                    {
                        OnMessage(carbon.Cate, carbon.Type, carbon.ObjMessage);
                    }
                }

                return PredefinedMessages.NoResponse;
            }

            protected bool _Disposed;
            public void Dispose()
            {
                if (!_Disposed)
                {
                    _Disposed = true;
                    if (OnClose != null)
                    {
                        OnClose();
                    }
                    OnClose = null;
                    OnMessage = null;
                    OnJsonMessage = null;
                }
            }
        }

        public static readonly ConnectionFactory.ConnectionConfig ConnectionConfig = new ConnectionFactory.ConnectionConfig()
        {
            SConfig = new SerializationConfig()
            {
                SplitterFactory = CarbonSplitter.Factory,
                Composer = new CarbonComposer(),
                ReaderWriter = new CarbonReaderAndWriter(),
            },
            ClientAttachmentCreators = new ConnectionFactory.IClientAttachmentCreator[]
            {
                new ConnectionFactory.ClientAttachmentCreator("Heartbeat", client => new Heartbeat(client, HeartbeatMessage)
                {
                    Timeout = -1,
                    Interval = 3000,
                }),
                //new ConnectionFactory.ClientAttachmentCreator("QoSHandler", client =>
                //{
                //    var handler = client as ReqHandler;
                //    if (handler != null)
                //    {
                //        handler.RegHandler(FuncQosHandler);
                //    }
                //    return null;
                //}),
                new ConnectionFactory.ClientAttachmentCreator("MessageHandler", client => new CarbonMessageHandler(client)),
            },
        };
        public static readonly ConnectionFactory.ConnectionConfig HostedPVPConnectionConfig = new ConnectionFactory.ConnectionConfig()
        {
            SConfig = new SerializationConfig()
            {
                SplitterFactory = CarbonSplitter.Factory,
                Composer = new CarbonComposer(),
                ReaderWriter = new CarbonReaderAndWriter(),
            },
            ClientAttachmentCreators = new ConnectionFactory.IClientAttachmentCreator[]
            {
                new ConnectionFactory.ClientAttachmentCreator("CarbonHeartbeat", client => new Heartbeat(client, HeartbeatMessage)
                {
                    Timeout = -1,
                    Interval = 3000,
                }),
                new ConnectionFactory.ClientAttachmentCreator("TokenSender", client =>
                {
                    SendToken((ReqClient)client, null);
                    return null;
                }),
            },
        };
        public static bool UseCarbonInPVP;
        public static ConnectionFactory.ConnectionConfig PVPConnectionConfig
        {
            get
            {
                if (UseCarbonInPVP)
                {
                    return HostedPVPConnectionConfig;
                }
                else
                {
                    return default(ConnectionFactory.ConnectionConfig);
                }
            }
        }

        //[EventOrder(80)]
        //public static object QosHandler(IReqClient from, uint type, object reqobj, uint seq)
        //{
        //    var carbonMessage = reqobj as CarbonMessage;
        //    if (carbonMessage != null && carbonMessage.ShouldPingback)
        //    {
        //        from.SendMessage(new CarbonMessage()
        //        {
        //            TraceIdHigh = carbonMessage.TraceIdHigh,
        //            TraceIdLow = carbonMessage.TraceIdLow,
        //        });
        //    }
        //    return null;
        //}
        //public static readonly Request.Handler FuncQosHandler = QosHandler;

        public static string CombineUrl(string host, int port)
        {
            string url = null;
            IPAddress address;
            if (IPAddress.TryParse(host, out address))
            {
                if (address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    url = "[" + host + "]";
                }
                else
                {
                    url = host;
                }
            }
            //else
            //{
            //    var addresses = Dns.GetHostAddresses(host);
            //    if (addresses != null && addresses.Length > 0)
            //    {
            //        address = addresses[0];
            //        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            //        {
            //            url = "[" + host + "]";
            //        }
            //        else
            //        {
            //            url = host;
            //        }
            //    }
            //}
            if (url == null)
            {
                url = host;
            }
            if (port > 0)
            {
                url += ":";
                url += port;
            }
            url = "tcp://" + url;
            return url;
        }
        public static ReqClient Connect(string url)
        {
            return ConnectionFactory.GetClient<ReqClient>(url, ConnectionConfig);
        }
        public static ReqClient Connect(string host, int port)
        {
            string url = CombineUrl(host, port);
            return Connect(url);
        }
        public static ReqClient ConnectWithDifferentPort(string url, int port)
        {
            var uri = new Uri(url);
            return Connect(uri.DnsSafeHost, port);
        }

        private static string _CurToken;
        public static void SendToken(ReqClient client, string token)
        {
            if (token == null)
            {
                if (_CurToken == null)
                {
                    return;
                }
                token = _CurToken;
            }
            else
            {
                _CurToken = token;
            }
            if (client != null)
            {
                byte[] message = null;
                if (token != null)
                {
                    var enc = System.Text.Encoding.UTF8.GetBytes(token);
                    if (enc.Length == 32)
                    {
                        message = enc;
                    }
                    else
                    {
                        message = new byte[32];
                        Buffer.BlockCopy(enc, 0, message, 0, Math.Min(message.Length, enc.Length));
                    }
                }
                if (message == null)
                {
                    message = new byte[32];
                }
                client.SendMessage(new CarbonMessage()
                {
                    Type = -1,
                    BytesMessage = message,
                });
            }
        }

        public static void OnClose(ReqClient client, Action onClose)
        {
            var handler = client.GetAttachment("MessageHandler") as CarbonMessageHandler;
            if (handler != null)
            {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
                if (onClose == null)
                {
                    handler.OnClose = null;
                }
                else
                {
                    if (ThreadSafeValues.IsMainThread)
                    {
                        handler.OnClose = () => UnityThreadDispatcher.RunInUnityThread(onClose);
                    }
                    else
                    {
                        handler.OnClose = onClose;
                    }
                }
#else
                handler.OnClose = onClose;
#endif
            }
        }
        public static void OnMessage(ReqClient client, Action<short, int, object> onMessage)
        {
            var handler = client.GetAttachment("MessageHandler") as CarbonMessageHandler;
            if (handler != null)
            {
                handler.OnMessage = onMessage;
            }
        }
        public static void OnJson(ReqClient client, Action<string, int> onJson)
        {
            var handler = client.GetAttachment("MessageHandler") as CarbonMessageHandler;
            if (handler != null)
            {
                handler.OnJsonMessage = onJson;
            }
        }
    }
}

#if UNITY_INCLUDE_TESTS
#region TESTS
#if UNITY_EDITOR
namespace Capstones.Net
{
    public static class CarbonMessageTest
    {
        public static string Url;
        public static string Token;
        public static ReqClient Client;

        public class CarbonMessageTestConnectToServer : UnityEditor.EditorWindow
        {
            [UnityEditor.MenuItem("Test/Carbon Message/Connect To...", priority = 200010)]
            static void Init()
            {
                GetWindow(typeof(CarbonMessageTestConnectToServer)).titleContent = new GUIContent("Input Url");
            }
            void OnGUI()
            {
                Url = UnityEditor.EditorGUILayout.TextField(Url);
                if (GUILayout.Button("Connect!"))
                {
                    Client = CarbonMessageUtils.Connect(Url);
                }
            }
        }
        public class CarbonMessageTestSendToken : UnityEditor.EditorWindow
        {
            [UnityEditor.MenuItem("Test/Carbon Message/Send Token...", priority = 200020)]
            static void Init()
            {
                GetWindow(typeof(CarbonMessageTestSendToken)).titleContent = new GUIContent("Input Token");
            }
            void OnGUI()
            {
                Token = UnityEditor.EditorGUILayout.TextField(Token);
                if (GUILayout.Button("Send!"))
                {
                    CarbonMessageUtils.SendToken(Client, Token);
                }
            }
        }
    }
}
#endif
#endregion
#endif

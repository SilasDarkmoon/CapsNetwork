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
    public class CarbonExFlags
    {
        public byte Flags;
        public ulong TraceIdHigh;
        public uint TraceIdLow;
        public byte Cate;
    }

    public class CarbonMessage
    {
        public byte Flags;
        public ulong TraceIdHigh;
        public uint TraceIdLow;
        public byte Cate;
        public ushort Type;

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
                Flags |= unchecked((byte)(0x1 << index));
            }
            else
            {
                Flags &= unchecked((byte)~(0x1 << index));
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
    /// 1 flags, 12 trace, 1 proto-category 2 message-type
    /// (12 trace pingback)
    /// </summary>
    public class CarbonSplitter : DataSplitter, IBuffered
    {
        public static readonly DataSplitterFactory Factory = new DataSplitterFactory<CarbonSplitter>();

        private NativeBufferStream _ReadBuffer = new NativeBufferStream();

        public CarbonSplitter() { }
        public CarbonSplitter(Stream input) : this()
        {
            Attach(input);
        }

        protected override void FireReceiveBlock(NativeBufferStream buffer, int size, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            ResetReadBlockContext();
            base.FireReceiveBlock(buffer, size, type, flags, seq, sseq, exFlags);
        }
        protected void ReadHeaders(out uint size, out byte flags, out ulong traceHigh, out uint traceLow, out byte cate, out ushort type)
        {
            // Read size.(4 bytes)
            size = 0;
            for (int i = 0; i < 4; ++i)
            {
                size <<= 8;
                size += (byte)_InputStream.ReadByte();
            }
            // Read flags.(1 byte)
            flags = (byte)_InputStream.ReadByte();
            // Read Trace Id.(12 bytes)
            traceHigh = 0;
            traceLow = 0;
            for (int i = 0; i < 8; ++i)
            {
                traceHigh <<= 8;
                traceHigh += (byte)_InputStream.ReadByte();
            }
            for (int i = 0; i < 4; ++i)
            {
                traceLow <<= 8;
                traceLow += (byte)_InputStream.ReadByte();
            }
            // Read Cate.(1 byte)
            cate = (byte)_InputStream.ReadByte();
            // Read Type. (2 byte)
            type = 0;
            for (int i = 0; i < 2; ++i)
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
                    byte flags;
                    ulong traceHigh;
                    uint traceLow;
                    byte cate;
                    ushort type;
                    ReadHeaders(out size, out flags, out traceHigh, out traceLow, out cate, out type);
                    int realsize = (int)(size - 16);
                    uint realtype = unchecked((uint)(int)(short)type);
                    var exFlags = new CarbonExFlags()
                    {
                        Flags = flags,
                        TraceIdHigh = traceHigh,
                        TraceIdLow = traceLow,
                        Cate = cate,
                    };
                    if (realsize >= 0)
                    {
                        if (realsize > CONST.MAX_MESSAGE_LENGTH)
                        {
                            PlatDependant.LogError("We got a too long message. We will drop this message and treat it as an error message.");
                            ProtobufEncoder.SkipBytes(_InputStream, realsize);
                            FireReceiveBlock(null, 0, realtype, flags, 0, 0, exFlags);
                        }
                        else
                        {
                            _ReadBuffer.Clear();
                            ProtobufEncoder.CopyBytes(_InputStream, _ReadBuffer, realsize);
                            FireReceiveBlock(_ReadBuffer, realsize, realtype, flags, 0, 0, exFlags);
                        }
                    }
                    else
                    {
                        FireReceiveBlock(null, 0, realtype, flags, 0, 0, exFlags);
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
        private uint _Type;
        private void ResetReadBlockContext()
        {
            _Size = 0;
            _Type = 0;
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
                            if (BufferedSize < 20)
                            {
                                return false;
                            }
                            uint size;
                            byte flags;
                            ulong traceHigh;
                            uint traceLow;
                            byte cate;
                            ushort type;
                            ReadHeaders(out size, out flags, out traceHigh, out traceLow, out cate, out type);
                            _ExFlags = new CarbonExFlags()
                            {
                                Flags = flags,
                                TraceIdHigh = traceHigh,
                                TraceIdLow = traceLow,
                                Cate = cate,
                            };
                            _Size = (int)(size - 16);
                            _Type = (uint)(int)(short)type;
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
                                    FireReceiveBlock(null, 0, _Type, _ExFlags.Flags, 0, 0, _ExFlags);
                                }
                                else
                                {
                                    _ReadBuffer.Clear();
                                    ProtobufEncoder.CopyBytes(_InputStream, _ReadBuffer, _Size);
                                    FireReceiveBlock(_ReadBuffer, _Size, _Type, _ExFlags.Flags, 0, 0, _ExFlags);
                                }
                            }
                            else
                            {
                                FireReceiveBlock(null, 0, _Type, _ExFlags.Flags, 0, 0, _ExFlags);
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

    public class CarbonComposer : DataComposer
    {
        public override void PrepareBlock(NativeBufferStream data, uint type, uint flags, uint seq, uint sseq, object exFlags)
        {
            if (data != null)
            {
                var size = data.Count;
                data.InsertMode = true;
                data.Seek(0, SeekOrigin.Begin);

                byte carbonFlags = 0;
                ulong carbonTraceIdHigh = 0;
                uint carbonTraceIdLow = 0;
                byte carbonCate = 0;
                CarbonExFlags carbonflags = exFlags as CarbonExFlags;
                if (carbonflags != null)
                {
                    carbonFlags = carbonflags.Flags;
                    carbonTraceIdHigh = carbonflags.TraceIdHigh;
                    carbonTraceIdLow = carbonflags.TraceIdLow;
                    carbonCate = carbonflags.Cate;
                }

                bool is_client = seq != 0;
                bool have_traceid = carbonTraceIdHigh != 0 || carbonTraceIdLow != 0;
                bool is_pingback = is_client && have_traceid;

                uint full_size = (uint)(size + 16);
                if (is_pingback) full_size += 12;

                // write size.(4 bytes) (not included in full_size)
                data.WriteByte((byte)((full_size & (0xFF << 24)) >> 24));
                data.WriteByte((byte)((full_size & (0xFF << 16)) >> 16));
                data.WriteByte((byte)((full_size & (0xFF << 8)) >> 8));
                data.WriteByte((byte)(full_size & 0xFF));
                // write flags.(1 byte)
                data.WriteByte((byte)(flags | carbonFlags));
                // Write TraceId (12 bytes)
                if (is_client)
                {
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                }
                else
                {
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 56)) >> 56));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 48)) >> 48));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 40)) >> 40));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 32)) >> 32));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 24)) >> 24));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 16)) >> 16));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 8)) >> 8));
                    data.WriteByte((byte)(carbonTraceIdHigh & 0xFFUL));
                    data.WriteByte((byte)((carbonTraceIdLow & (0xFF << 24)) >> 24));
                    data.WriteByte((byte)((carbonTraceIdLow & (0xFF << 16)) >> 16));
                    data.WriteByte((byte)((carbonTraceIdLow & (0xFF << 8)) >> 8));
                    data.WriteByte((byte)(carbonTraceIdLow & 0xFF));
                }
                // Write Cate.(1 byte)
                data.WriteByte(carbonCate);
                // Write Type.(2 bytes)
                data.WriteByte((byte)((type & (0xFF << 8)) >> 8));
                data.WriteByte((byte)(type & 0xFF));
                // write Traceback.(12 bytes)
                if (is_pingback)
                {
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 56)) >> 56));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 48)) >> 48));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 40)) >> 40));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 32)) >> 32));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 24)) >> 24));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 16)) >> 16));
                    data.WriteByte((byte)((carbonTraceIdHigh & (0xFFUL << 8)) >> 8));
                    data.WriteByte((byte)(carbonTraceIdHigh & 0xFFUL));
                    data.WriteByte((byte)((carbonTraceIdLow & (0xFF << 24)) >> 24));
                    data.WriteByte((byte)((carbonTraceIdLow & (0xFF << 16)) >> 16));
                    data.WriteByte((byte)((carbonTraceIdLow & (0xFF << 8)) >> 8));
                    data.WriteByte((byte)(carbonTraceIdLow & 0xFF));
                }
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
                    TraceIdHigh = carbonmess.TraceIdHigh,
                    TraceIdLow = carbonmess.TraceIdLow,
                    Cate = carbonmess.Cate,
                };
            }
            else
            {
                return base.GetExFlags(data);
            }
        }
        public override uint GetDataType(object data)
        {
            CarbonMessage carbonmess = data as CarbonMessage;
            if (carbonmess != null)
            {
                return (uint)(int)carbonmess.Type;
            }
            else
            {
                return base.GetDataType(data);
            }
        }
        public override NativeBufferStream Write(object data)
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
        public override object Read(uint type, NativeBufferStream buffer, int offset, int cnt, object exFlags)
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
                    TraceIdHigh = carbonFlags.TraceIdHigh,
                    TraceIdLow = carbonFlags.TraceIdLow,
                    Cate = carbonFlags.Cate,
                    Type = (ushort)(short)(int)type,
                };
                if (carbonFlags.Cate == 3)
                { // Json
                    byte[] raw = PredefinedMessages.GetRawBuffer(cnt);
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
                    message.StrMessage = str;
                }
                else if (carbonFlags.Cate == 4)
                { // PB
                    message.ObjMessage = base.Read(type, buffer, offset, cnt, exFlags);
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
            public Action<byte, ushort, object> OnMessage;
            public Action<string, ushort> OnJsonMessage;

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

        public static readonly ConnectionFactory.ExtendedConfig ConnectionConfig = new ConnectionFactory.ExtendedConfig()
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
                new ConnectionFactory.ClientAttachmentCreator("QoSHandler", client =>
                {
                    var handler = client as ReqHandler;
                    if (handler != null)
                    {
                        handler.RegHandler(FuncQosHandler);
                    }
                    return null;
                }),
                new ConnectionFactory.ClientAttachmentCreator("MessageHandler", client => new CarbonMessageHandler(client)),
            },
        };

        [EventOrder(80)]
        public static object QosHandler(IReqClient from, uint type, object reqobj, uint seq)
        {
            var carbonMessage = reqobj as CarbonMessage;
            if (carbonMessage != null && carbonMessage.ShouldPingback)
            {
                from.SendMessage(new CarbonMessage()
                {
                    TraceIdHigh = carbonMessage.TraceIdHigh,
                    TraceIdLow = carbonMessage.TraceIdLow,
                });
            }
            return null;
        }
        public static readonly Request.Handler FuncQosHandler = QosHandler;

        public static ReqClient Connect(string url)
        {
            return ConnectionFactory.GetClient<ReqClient>(url, ConnectionConfig);
        }
        public static ReqClient Connect(string host, int port)
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
            return Connect(url);
        }
        public static ReqClient ConnectWithDifferentPort(string url, int port)
        {
            var uri = new Uri(url);
            return Connect(uri.DnsSafeHost, port);
        }

        public static void SendToken(ReqClient client, string token)
        {
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
                    Type = unchecked((ushort)-1),
                    BytesMessage = message,
                });
            }
        }

        public static void OnClose(ReqClient client, Action onClose)
        {
            var handler = client.GetAttachment("MessageHandler") as CarbonMessageHandler;
            if (handler != null)
            {
                handler.OnClose = onClose;
            }
        }
        public static void OnMessage(ReqClient client, Action<byte, ushort, object> onMessage)
        {
            var handler = client.GetAttachment("MessageHandler") as CarbonMessageHandler;
            if (handler != null)
            {
                handler.OnMessage = onMessage;
            }
        }
        public static void OnJson(ReqClient client, Action<string, ushort> onJson)
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

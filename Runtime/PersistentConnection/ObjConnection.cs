using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;
using System.IO;
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
using UnityEngine;
#if !NET_4_6 && !NET_STANDARD_2_0
using Unity.Collections.Concurrent;
#else
using System.Collections.Concurrent;
#endif
#else
using System.Collections.Concurrent;
#endif

using PlatDependant = Capstones.UnityEngineEx.PlatDependant;
using TaskProgress = Capstones.UnityEngineEx.TaskProgress;

namespace Capstones.Net
{
    public class SerializationConfig : ICloneable
    {
        public Func<Stream, DataSplitter> SplitterFactory;
        public DataComposer Composer;
        public DataReaderAndWriter ReaderWriter;
        protected internal readonly List<DataPostProcess> PostProcessors = new List<DataPostProcess>();

        public void RemovePostProcess<T>() where T : DataPostProcess
        {
            for (int i = 0; i < PostProcessors.Count; ++i)
            {
                if (PostProcessors[i] is T)
                {
                    PostProcessors.RemoveAt(i--);
                }
            }
        }
        public void AddPostProcess(DataPostProcess processor)
        {
            PostProcessors.Add(processor);
            PostProcessors.Sort((a, b) => a.Order - b.Order);
        }
        public void AddPostProcessors(params DataPostProcess[] processors)
        {
            if (processors != null && processors.Length > 0)
            {
                PostProcessors.AddRange(processors);
                PostProcessors.Sort((a, b) => a.Order - b.Order);
            }
        }
        public void ClearPostProcess()
        {
            PostProcessors.Clear();
        }

        public SerializationConfig Clone()
        {
            var cloned = new SerializationConfig() { SplitterFactory = SplitterFactory, Composer = Composer, ReaderWriter = ReaderWriter };
            cloned.PostProcessors.AddRange(PostProcessors);
            return cloned;
        }
        object ICloneable.Clone()
        {
            return Clone();
        }
    }

    public class ObjClient : IDisposable
    {
        protected struct PendingRead
        {
            public uint Type;
            public object Obj;
            public uint Seq;
            public uint SSeq;
        }

        protected IPersistentConnection _Client;
        protected ConnectionStream _Stream;
        protected DataSplitter _Splitter;
        protected SerializationConfig _SerConfig;
        protected PendingRead _PendingRead;
        protected int _LastReceiveTick;

        public bool LeaveOpen = false;
        public ConnectionStream Stream { get { return _Stream; } }
        public int LastReceiveTick { get { return _LastReceiveTick; } }

        public ObjClient(
            string url
            , Func<string, IPersistentConnection> clientFactory
            , SerializationConfig sconfig)
        {
            _SerConfig = sconfig;
            _Client = clientFactory(url);
            _Stream = new ConnectionStream(_Client);
            _Splitter = sconfig.SplitterFactory(_Stream);
            _Splitter.OnReceiveBlock += ReceiveBlock;
            _Client.StartConnect();
            _LastReceiveTick = System.Environment.TickCount;
        }

        protected void ReceiveBlock(NativeBufferStream buffer, int size, uint type, uint flags, uint seq, uint sseq)
        {
            _LastReceiveTick = System.Environment.TickCount;
            if (buffer != null && size >= 0 && size <= buffer.Length)
            {
                var processors = _SerConfig.PostProcessors;
                for (int i = processors.Count - 1; i >= 0; --i)
                {
                    var processor = processors[i];
                    var pack = processor.Deprocess(buffer, 0, size, flags, type, seq, sseq, _IsServer);
                    flags = pack.t1;
                    size = Math.Max(Math.Min(pack.t2, size), 0);
                }
                _PendingRead.Type = type;
                _PendingRead.Obj = _SerConfig.ReaderWriter.Read(type, buffer, 0, size);
                _PendingRead.Seq = seq;
                _PendingRead.SSeq = sseq;
                OnReceiveObj(_PendingRead.Obj, type, seq, sseq);
            }
        }
        public delegate void ReceiveObjAction(object obj, uint type, uint seq, uint sseq);
        public event ReceiveObjAction OnReceiveObj = (obj, type, seq, sseq) => { };
        public object TryRead(out uint seq, out uint sseq, out uint type)
        {
            try
            {
                while (_Client != null && _Client.IsConnectionAlive && _Splitter.TryReadBlock())
                {
                    if (_PendingRead.Obj != null)
                    {
                        var obj = _PendingRead.Obj;
                        seq = _PendingRead.Seq;
                        sseq = _PendingRead.SSeq;
                        type = _PendingRead.Type;
                        _PendingRead.Obj = null;
                        return obj;
                    }
                }
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
            }
            seq = 0;
            sseq = 0;
            type = 0;
            return null;
        }
        public object TryRead(out uint seq, out uint sseq)
        {
            uint type;
            return TryRead(out seq, out sseq, out type);
        }
        public object TryRead(out uint seq)
        {
            uint sseq;
            var obj = TryRead(out seq, out sseq);
            if (!_IsServer)
            {
                seq = sseq;
            }
            return obj;
        }
        public object TryRead()
        {
            uint seq, sseq;
            return TryRead(out seq, out sseq);
        }

        public object Read(out uint seq, out uint sseq, out uint type)
        {
            try
            {
                while (_Client != null && _Client.IsConnectionAlive)
                {
                    _Splitter.ReadBlock();
                    if (_PendingRead.Obj != null)
                    {
                        var obj = _PendingRead.Obj;
                        seq = _PendingRead.Seq;
                        sseq = _PendingRead.SSeq;
                        type = _PendingRead.Type;
                        _PendingRead.Obj = null;
                        return obj;
                    }
                }
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
            }
            seq = 0;
            sseq = 0;
            type = 0;
            return null;
        }
        public object Read(out uint seq, out uint sseq)
        {
            uint type;
            return Read(out seq, out sseq, out type);
        }
        public object Read(out uint seq)
        {
            uint sseq;
            var obj = Read(out seq, out sseq);
            if (!_IsServer)
            {
                seq = sseq;
            }
            return obj;
        }
        public object Read()
        {
            uint seq, sseq;
            return Read(out seq, out sseq);
        }

        protected int _NextSeq = 1;
        public uint NextSeq
        {
            get { return (uint)_NextSeq; }
            set { _NextSeq = (int)value; }
        }
        protected internal bool _IsServer = false;
        public bool IsServer { get { return _IsServer; } }
        private bool _OldIsConnected;
        public bool IsConnected
        {
            get
            {
                if (_Client is IServerConnection)
                {
                    var value = ((IServerConnection)_Client).IsConnected;
                    if (value && !_OldIsConnected)
                    {
                        _OldIsConnected = value;
                        _LastReceiveTick = System.Environment.TickCount;
                    }
                    return value;
                }
                else
                {
                    return true;
                }
            }
        }
        public bool IsConnectionAlive
        {
            get { return _Client != null && _Client.IsConnectionAlive; }
        }
        public EndPoint RemoteEndPoint
        {
            get { return _Client == null ? null : _Client.RemoteEndPoint; }
        }
        public void Write(object obj)
        {
            Write(obj, 0);
        }
        public void Write(object obj, uint seq_pingback)
        {
            Write(obj, seq_pingback, 0);
        }
        public void Write(object obj, uint seq_pingback, uint flags)
        {
            // type
            var rw = _SerConfig.ReaderWriter;
            var type = rw.GetDataType(obj);
            // seq
            uint seq = 0, sseq = 0;
            if (_IsServer)
            {
                seq = seq_pingback;
                sseq = (uint)Interlocked.Increment(ref _NextSeq) - 1;
            }
            else
            {
                seq = (uint)Interlocked.Increment(ref _NextSeq) - 1;
                sseq = seq_pingback;
            }
            // write obj
            var stream = rw.Write(obj);
            if (stream != null)
            {
                // post process (encrypt etc.)
                var processors = _SerConfig.PostProcessors;
                for (int i = 0; i < processors.Count; ++i)
                {
                    var processor = processors[i];
                    flags = processor.Process(stream, 0, flags, type, seq, sseq, _IsServer);
                }
                // compose block
                _SerConfig.Composer.PrepareBlock(stream, type, flags, seq, sseq);
                // send
                _Stream.Write(stream, 0, stream.Count);
            }
        }

#region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (!LeaveOpen)
                {
                    _Stream.Dispose();
                }
                _Client = null;
                _Stream = null;
                if (_Splitter != null)
                {
                    _Splitter.Dispose();
                    _Splitter = null;
                }
                _SerConfig = null;
                _PendingRead.Obj = null;
                disposedValue = true;
            }
        }
        ~ObjClient()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
#endregion
    }

    public class ObjServer : IDisposable
    {
        protected IPersistentConnectionServer _Server;
        protected SerializationConfig _SerConfig;

        public ObjServer(
            string url
            , Func<string, IPersistentConnectionServer> serverFactory
            , SerializationConfig sconfig)
        {
            _SerConfig = sconfig;
            _Server = serverFactory(url);
            _Server.StartListening();
        }

        protected IPersistentConnection CreateServerConnection(string url)
        {
            return _Server.PrepareConnection();
        }

        public ObjClient GetConnection()
        {
            return new ObjClient(null, CreateServerConnection, _SerConfig) { _IsServer = true };
        }

#region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (_Server is IDisposable)
                {
                    ((IDisposable)_Server).Dispose();
                }
                _Server = null;
                _SerConfig = null;
                disposedValue = true;
            }
        }
        ~ObjServer()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
#endregion
    }

    public struct PersistentConnectionResponseData
    {
        public uint RespDataType;
        public uint RespSeq;
        public object Result;
    }
    public class PersistentConnectionRequest
    {
        // request data
        protected uint _Seq;
        public uint Seq { get { return _Seq; } }
        protected readonly object _Data;
        public object Data { get { return _Data; } }
        // response data
        protected bool _Done;
        public bool Done { get { return _Done; } }
        protected internal PersistentConnectionResponseData _Resp;
        public uint RespReq { get { return _Resp.RespSeq; } }
        public object Result { get { return _Resp.Result; } }
        public uint RespDataType { get { return _Resp.RespDataType; } }
        // timeout and error
        protected internal string _Error;
        public string Error { get { return _Error; } }
        public int Timeout = CONST.DEFAULT_TIMEOUT;
        protected int _StartTick;
        public int StartTick { get { return _StartTick; } }
        protected int _DoneTick;
        public int DoneTick { get { return _DoneTick; } }
        public int RTT { get { return _DoneTick - _StartTick; } }

        protected internal PersistentConnectionRequest(object data)
        {
            _Data = data;
            _StartTick = Environment.TickCount;
        }

        protected internal void Send(ObjClient con)
        {
            _Seq = con.NextSeq;
            con.Write(_Data);
        }
        protected internal void Receive(object result)
        {
            _DoneTick = Environment.TickCount;
            _Resp.Result = result;
            _Done = true;
        }
    }
    public abstract class PersistentConnectionRequestFactoryBase : IDisposable
    {
        protected ObjClient _Connection;
        protected bool _ShouldLock;
        protected readonly LinkedList<PersistentConnectionRequest> _PendingRequests = new LinkedList<PersistentConnectionRequest>();

        public delegate bool FilterMessageFunc(uint type, uint seq, object raw);
        protected readonly List<FilterMessageFunc> _FilterMessageHandlers = new List<FilterMessageFunc>();
        public event FilterMessageFunc OnFilterMessage
        {
            add
            {
                _FilterMessageHandlers.Add(value);
            }
            remove
            {
                for (int i = 0; i < _FilterMessageHandlers.Count; ++i)
                {
                    if (_FilterMessageHandlers[i] == value)
                    {
                        _FilterMessageHandlers.RemoveAt(i--);
                    }
                }
            }
        }
        protected bool FilterMessage(uint type, uint seq, object raw)
        {
            for (int i = 0; i < _FilterMessageHandlers.Count; ++i)
            {
                if (_FilterMessageHandlers[i] != null && !_FilterMessageHandlers[i](type, seq, raw))
                {
                    return false;
                }
            }
            return true;
        }
        //public delegate void ReceiveMessageAction(PersistentConnectionResponseData mess);
        //public event ReceiveMessageAction OnReceiveMessage = mess => { };

        public PersistentConnectionRequestFactoryBase(ObjClient con)
        {
            _Connection = con;
        }
        protected void Update()
        {
            object readobj = null;
            uint type, seq, sseq;
            while ((readobj = Read(out seq, out sseq, out type)) != null)
            {
                uint reqseq;
                uint respseq;
                if (_Connection.IsServer)
                {
                    reqseq = sseq;
                    respseq = seq;
                }
                else
                {
                    reqseq = seq;
                    respseq = sseq;
                }
                if (reqseq != 0)
                {
                    if (_ShouldLock)
                    {
                        lock (_PendingRequests)
                        {
                            CheckPendingRequests(readobj, type, reqseq, respseq);
                        }
                    }
                    else
                    {
                        CheckPendingRequests(readobj, type, reqseq, respseq);
                    }
                }
                else
                {
                    if (PushMessageCount >= CONST.MAX_QUEUED_MESSAGE)
                    {
                        PlatDependant.LogError("To many unhandled push messages. Do you forget to check push messages?");
                        while (PushMessageCount >= CONST.MAX_QUEUED_MESSAGE)
                        {
                            PersistentConnectionResponseData oldmess;
                            if (!TryDequeuePushMessage(out oldmess))
                            {
                                break;
                            }
                        }
                    }
                    //else
                    {
                        if (FilterMessage(type, respseq, readobj))
                        {
                            var mess = new PersistentConnectionResponseData()
                            {
                                RespDataType = type,
                                RespSeq = respseq,
                                Result = readobj,
                            };
                            EnqueuePushMessage(mess);
                            //OnReceiveMessage(mess);
                        }
                    }
                }
            }
        }
        protected void CheckPendingRequests(object readobj, uint type, uint reqseq, uint respseq)
        {
            LinkedListNode<PersistentConnectionRequest> node = _PendingRequests.First;
            while (node != null)
            {
                var req = node.Value;
                if (reqseq == req.Seq)
                {
                    req._Resp.RespDataType = type;
                    req._Resp.RespSeq = respseq;
                    req.Receive(readobj);
                    RecordRequestRTT(req.RTT);
                    _PendingRequests.Remove(node);
                    break;
                }
                else
                {
                    var tick = Environment.TickCount;
                    var timeout = req.Timeout;
                    if (timeout == 0)
                    {
                        timeout = CONST.DEFAULT_TIMEOUT;
                    }
                    if (timeout > 0 && tick - req.StartTick > timeout)
                    {
                        req._Error = "timedout";
                        req.Receive(null);
                        RecordRequestRTT(timeout);
                        var next = node.Next;
                        _PendingRequests.Remove(node);
                        node = next;
                    }
                    else
                    {
                        node = node.Next;
                    }
                }
            }
        }
#region RTT Timing
        protected int _RTT = 0;
        public int RTT { get { return _RTT; } }
        protected const int _TimedRequestCount = 4;
        protected int[] _TimedRequestsRTT = new int[_TimedRequestCount];
        protected int _LastTimedRequestIndex = _TimedRequestCount - 1;
        protected void RecordRequestRTT(int rtt)
        {
            _LastTimedRequestIndex = (_LastTimedRequestIndex + 1) % _TimedRequestCount;
            _TimedRequestsRTT[_LastTimedRequestIndex] = rtt;
            int count = 0;
            int total = 0;
            for (int i = 0; i < _TimedRequestsRTT.Length; ++i)
            {
                var time = _TimedRequestsRTT[i];
                if (time > 0)
                {
                    ++count;
                    total += time;
                }
            }
            if (count > 0)
            {
                _RTT = total / count;
            }
            else
            {
                _RTT = 0;
            }
        }
#endregion

        protected abstract object Read(out uint seq, out uint sseq, out uint type);
        protected abstract int PushMessageCount { get; }
        protected abstract void EnqueuePushMessage(PersistentConnectionResponseData data);
        protected abstract bool TryDequeuePushMessage(out PersistentConnectionResponseData data);
        protected abstract void DoSendRequest(PersistentConnectionRequest req);
        protected abstract void DoSendMessage(object obj, uint seq_pingback);

        public void SendMessage(object obj)
        {
            SendMessage(obj, 0);
        }
        public void SendMessage(object obj, uint seq_pingback)
        {
            DoSendMessage(obj, seq_pingback);
        }
        public PersistentConnectionRequest SendRequest(object obj)
        {
            var req = new PersistentConnectionRequest(obj);
            DoSendRequest(req); //req.Send(_Connection);
            if (_ShouldLock)
            {
                lock (_PendingRequests)
                {
                    _PendingRequests.AddLast(req);
                }
            }
            else
            {
                _PendingRequests.AddLast(req);
            }
            return req;
        }
        public virtual PersistentConnectionResponseData GetMessageInfo()
        {
            PersistentConnectionResponseData message;
            if (TryDequeuePushMessage(out message))
            {
                return message;
            }
            return default(PersistentConnectionResponseData);
        }
        public object GetMessage()
        {
            PersistentConnectionResponseData message = GetMessageInfo();
            return message.Result;
        }

        protected virtual void OnDispose() { }
        public void Dispose()
        {
            if (_Connection != null)
            {
                _Connection.Dispose();
                _Connection = null;
            }
            _FilterMessageHandlers.Clear();
            OnDispose();
        }
    }
    public class PersistentConnectionRequestFactory : PersistentConnectionRequestFactoryBase
    {
        protected readonly ConcurrentQueueGrowOnly<PersistentConnectionResponseData> _PushMessages = new ConcurrentQueueGrowOnly<PersistentConnectionResponseData>();
        protected struct PendingSendData
        {
            public PersistentConnectionRequest _Req;
            public object _Raw;
            public uint _SeqPingBack;
        }
        protected readonly ConcurrentQueueGrowOnly<PendingSendData> _PendingSend = new ConcurrentQueueGrowOnly<PendingSendData>();
        protected AutoResetEvent _HaveDataToSend = new AutoResetEvent(false);
        protected volatile bool _WriteDone = false;

        public PersistentConnectionRequestFactory(ObjClient con) : base(con)
        {
            _ShouldLock = true;
            PlatDependant.RunBackground(ReadWork);
            PlatDependant.RunBackground(WriteWork);
        }
        protected void ReadWork(TaskProgress prog)
        {
            Update();
            _WriteDone = true;
            _HaveDataToSend.Set();
        }
        protected void WriteWork(TaskProgress prog)
        {
            try
            {
                while (_HaveDataToSend.WaitOne())
                {
                    try
                    {
                        if (_Connection == null || !_Connection.IsConnectionAlive || _WriteDone)
                        {
                            return;
                        }
                        PendingSendData pending;
                        while (_PendingSend.TryDequeue(out pending))
                        {
                            if (pending._Req != null)
                            {
                                pending._Req.Send(_Connection);
                            }
                            else if (pending._Raw != null)
                            {
                                _Connection.Write(pending._Raw, pending._SeqPingBack);
                            }
                        }
                    }
                    catch (ThreadAbortException)
                    {
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                Thread.ResetAbort();
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
            }
        }

        protected override object Read(out uint seq, out uint sseq, out uint type)
        {
            return _Connection.Read(out seq, out sseq, out type);
        }
        protected override int PushMessageCount { get { return _PushMessages.Count; } }
        protected override void EnqueuePushMessage(PersistentConnectionResponseData data)
        {
            _PushMessages.Enqueue(data);
        }
        protected override bool TryDequeuePushMessage(out PersistentConnectionResponseData data)
        {
            return _PushMessages.TryDequeue(out data);
        }
        protected override void DoSendRequest(PersistentConnectionRequest req)
        {
            _PendingSend.Enqueue(new PendingSendData() { _Req = req });
            _HaveDataToSend.Set();
        }
        protected override void DoSendMessage(object obj, uint seq_pingback)
        {
            _PendingSend.Enqueue(new PendingSendData() { _Raw = obj, _SeqPingBack = seq_pingback });
            _HaveDataToSend.Set();
        }
        protected override void OnDispose()
        {
            _WriteDone = true;
            _HaveDataToSend.Set();
        }
    }
    public class PersistentConnectionRequestFactoryMainThread : PersistentConnectionRequestFactoryBase
    {
        protected readonly Queue<PersistentConnectionResponseData> _PushMessages = new Queue<PersistentConnectionResponseData>();

        public PersistentConnectionRequestFactoryMainThread(ObjClient con) : base(con)
        {
            _ShouldLock = false;
        }

        protected override object Read(out uint seq, out uint sseq, out uint type)
        {
            return _Connection.TryRead(out seq, out sseq, out type);
        }
        protected override int PushMessageCount { get { return _PushMessages.Count; } }
        protected override void EnqueuePushMessage(PersistentConnectionResponseData data)
        {
            _PushMessages.Enqueue(data);
        }
        protected override bool TryDequeuePushMessage(out PersistentConnectionResponseData data)
        {
            if (_PushMessages.Count > 0)
            {
                data = _PushMessages.Dequeue();
                return true;
            }
            else
            {
                data = default(PersistentConnectionResponseData);
                return false;
            }
        }
        protected override void DoSendRequest(PersistentConnectionRequest req)
        {
            req.Send(_Connection);
        }
        protected override void DoSendMessage(object obj, uint seq_pingback)
        {
            _Connection.Write(obj, seq_pingback);
        }

        public override PersistentConnectionResponseData GetMessageInfo()
        {
            Update();
            return base.GetMessageInfo();
        }
    }

    public static partial class PersistentConnectionFactory
    {
        private struct PersistentConnectionCreator
        {
            public Func<string, IPersistentConnection> ClientCreator;
            public Func<string, IPersistentConnectionServer> ServerCreator;

        }
        private static Dictionary<string, PersistentConnectionCreator> _Creators;
        private static Dictionary<string, PersistentConnectionCreator> Creators
        {
            get
            {
                if (_Creators == null)
                {
                    _Creators = new Dictionary<string, PersistentConnectionCreator>();
                }
                return _Creators;
            }
        }
        private class RegisteredCreator
        {
            public RegisteredCreator(string scheme, Func<string, IPersistentConnection> clientFactory, Func<string, IPersistentConnectionServer> serverFactory)
            {
                Creators[scheme] = new PersistentConnectionCreator() { ClientCreator = clientFactory, ServerCreator = serverFactory };
            }
        }

        private static readonly SerializationConfig _InnerDefaultSerializationConfig = new SerializationConfig()
        {
            SplitterFactory = stream => new ProtobufSplitter(stream),
            Composer = new ProtobufComposer(),
            ReaderWriter = new ProtobufReaderAndWriter(),
        };
        private static SerializationConfig _DefaultSerializationConfig = null;
        public static SerializationConfig DefaultSerializationConfig
        {
            get { return _DefaultSerializationConfig ?? _InnerDefaultSerializationConfig; }
            set { _DefaultSerializationConfig = value; }
        }

        public static ObjServer GetServer(string url, SerializationConfig sconfig)
        {
            var uri = new Uri(url);
            var scheme = uri.Scheme;
            PersistentConnectionCreator creator;
            if (Creators.TryGetValue(scheme, out creator))
            {
                if (creator.ServerCreator != null)
                {
                    return new ObjServer(url, creator.ServerCreator, sconfig);
                }
            }
            return null;
        }
        public static ObjServer GetServer(string url)
        {
            return GetServer(url, DefaultSerializationConfig);
        }
        public static ObjClient GetClient(string url, SerializationConfig sconfig)
        {
            var uri = new Uri(url);
            var scheme = uri.Scheme;
            PersistentConnectionCreator creator;
            if (Creators.TryGetValue(scheme, out creator))
            {
                if (creator.ClientCreator != null)
                {
                    return new ObjClient(url, creator.ClientCreator, sconfig);
                }
            }
            return null;
        }
        public static ObjClient GetClient(string url)
        {
            return GetClient(url, DefaultSerializationConfig);
        }
    }
}

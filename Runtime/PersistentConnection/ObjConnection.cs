﻿using System;
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
        public DataSplitterFactory SplitterFactory;
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

        public static readonly SerializationConfig Default = new SerializationConfig()
        {
            SplitterFactory = ProtobufSplitter.Factory,
            Composer = new ProtobufComposer(),
            ReaderWriter = new ProtobufReaderAndWriter(),
        };
    }

    public interface IChannel
    {
        void Start();
        bool IsStarted { get; }
        bool IsAlive { get; }

        event Action OnUpdate;
        event Action<IChannel> OnConnected;
        event Action OnClose;
    }

    public class ObjClient : IPositiveConnection, IChannel, IDisposable
    {
        protected struct PendingRead
        {
            public uint Type;
            public object Obj;
            public uint Seq;
            public uint SSeq;
        }

        protected IPersistentConnection _Connection;
        protected IServerConnection _ServerConnection;
        protected ConnectionStream _Stream;
        protected DataSplitter _Splitter;
        protected SerializationConfig _SerConfig;
        protected bool _DeserializeInConnectionThread = false;
        public bool DeserializeInConnectionThread { get { return _DeserializeInConnectionThread; } }
        protected bool _SerializeInConnectionThread = false;
        public bool SerializeInConnectionThread { get { return _SerializeInConnectionThread; } }

        public bool LeaveOpen = false;
        public IPersistentConnection Connection { get { return _Connection; } }
        public ConnectionStream Stream { get { return _Stream; } }

        protected int _NextSeq = 1;
        protected internal uint NextSeq
        {
            get { return (uint)_NextSeq; }
            set { _NextSeq = (int)value; }
        }

        protected internal bool? _IsServer;
        public bool IsServer
        {
            get
            {
                if (_IsServer != null)
                {
                    return _IsServer ?? false;
                }
                if (_Connection is IServerConnection)
                {
                    _IsServer = true;
                    return true;
                }
                else
                {
                    _IsServer = false;
                    return false;
                }
            }
        }

        protected bool _Started;
        public bool IsStarted { get { return IsConnected; } }
        public bool IsConnected
        {
            get
            {
                if (_ServerConnection != null)
                {
                    return _Started && _ServerConnection.IsConnected;
                }
                else
                {
                    return _Started;
                }
            }
        }
        public event Action<IChannel> OnConnected;
        protected void FireOnConnected()
        {
            if (_ServerConnection != null)
            {
                _ServerConnection.OnConnected -= FireOnConnected;
            }
            if (OnConnected != null)
            {
                OnConnected(this);
            }
        }
        public bool IsAlive
        {
            get { return _Connection != null && _Connection.IsAlive; }
        }
        public EndPoint RemoteEndPoint
        {
            get { return _Connection == null ? null : _Connection.RemoteEndPoint; }
        }

        public ObjClient(IPersistentConnection connection, SerializationConfig sconfig, IDictionary<string, object> exconfig)
        {
            _DeserializeInConnectionThread = ConfigManager.Get<bool>(exconfig, "DeserializeInConnectionThread");
            _SerializeInConnectionThread = ConfigManager.Get<bool>(exconfig, "SerializeInConnectionThread");
            var idletimeout = ConfigManager.Get<int>(exconfig, "IdleTimeout");
            if (idletimeout != 0)
            {
                IdleTimeout = idletimeout;
            }
            _SerConfig = sconfig;
            _Connection = connection;
            _LastReceiveTick = Environment.TickCount;
            _Connection.OnUpdate = OnConnectionUpdate;
            _ServerConnection = connection as IServerConnection;
            _PositiveConnection = connection as IPositiveConnection;
            _Stream = new ConnectionStream(_Connection, true) { DonotNotifyReceive = !_DeserializeInConnectionThread };
            _Splitter = sconfig.SplitterFactory.Create(_Stream);
            _Splitter.OnReceiveBlock += ReceiveBlock;
            _SendSerializer = SerializeMessage;
        }
        public ObjClient(IPersistentConnection connection, SerializationConfig sconfig)
            : this(connection, sconfig, null)
        { }
        public ObjClient(IPersistentConnection connection)
            : this(connection, SerializationConfig.Default, null)
        { }

        public void Start()
        {
            if (!_Started)
            {
                if (_ServerConnection != null)
                {
                    _ServerConnection.OnConnected += FireOnConnected;
                }
                _Connection.StartConnect();
                _Started = true;
                if (_ServerConnection == null)
                {
                    FireOnConnected();
                }
            }
        }

        protected int _LastReceiveTick;
        public int IdleTimeout = -1;
        protected int OnConnectionUpdate(IPersistentConnection connection)
        {
            if (OnUpdate != null)
            {
                OnUpdate();
            }
            if (!IsConnected)
            {
                _LastReceiveTick = Environment.TickCount;
            }
            else
            {
#if !DEBUG_PERSIST_CONNECT_NO_IDLE_TIMEOUT
                var timeout = IdleTimeout;
                if (timeout >= 0)
                {
                    var idletime = Environment.TickCount - _LastReceiveTick;
                    if (idletime >= timeout)
                    {
                        PlatDependant.LogError("ObjClient Idle Timedout.");
                        Dispose();
                    }
                }
#endif
            }
            return int.MinValue;
        }
        public event Action OnUpdate;

#region Read
        protected ConcurrentQueueGrowOnly<PendingRead> _PendingReadQueue = new ConcurrentQueueGrowOnly<PendingRead>();
        protected PendingRead _PendingRead;
        protected internal AutoResetEvent _WaitForObjRead = new AutoResetEvent(false);
        protected void ReceiveBlock(NativeBufferStream buffer, int size, uint type, uint flags, uint seq, uint sseq)
        {
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
            PlatDependant.LogError(Environment.TickCount.ToString() + $" Receive(size{size} type{type} seq{seq} sseq{sseq})");
#endif
            _LastReceiveTick = Environment.TickCount;
            if (buffer != null && size >= 0 && size <= buffer.Length)
            {
                var processors = _SerConfig.PostProcessors;
                for (int i = processors.Count - 1; i >= 0; --i)
                {
                    var processor = processors[i];
                    var pack = processor.Deprocess(buffer, 0, size, flags, type, seq, sseq, IsServer);
                    flags = pack.t1;
                    size = Math.Max(Math.Min(pack.t2, size), 0);
                }
                var pending = new PendingRead()
                {
                    Type = type,
                    Obj = _SerConfig.ReaderWriter.Read(type, buffer, 0, size),
                    Seq = seq,
                    SSeq = sseq,
                };
                if (!_DeserializeInConnectionThread)
                {
                    _PendingRead = pending;
                    OnReceiveObj(pending.Obj, type, seq, sseq);
                }
                else
                {
                    var queue = _PendingReadQueue;
                    if (queue != null)
                    {
                        queue.Enqueue(pending);
                    }
                    OnReceiveObj(pending.Obj, type, seq, sseq);
                    _WaitForObjRead.Set();
                }
            }
        }
        public delegate void ReceiveObjAction(object obj, uint type, uint seq, uint sseq);
        public event ReceiveObjAction OnReceiveObj = (obj, type, seq, sseq) => { };
        public object TryRead(out uint seq, out uint sseq, out uint type)
        {
            if (!_DeserializeInConnectionThread)
            {
                try
                {
                    while (_Connection != null && _Connection.IsAlive && _Splitter.TryReadBlock())
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
            else
            {
                PendingRead pending;
                var queue = _PendingReadQueue;
                if (queue != null)
                {
                    if (_PendingReadQueue.TryDequeue(out pending))
                    {
                        seq = pending.Seq;
                        sseq = pending.SSeq;
                        type = pending.Type;
                        return pending.Obj;
                    }
                }
                seq = 0;
                sseq = 0;
                type = 0;
                return null;
            }
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
            if (!IsServer)
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
            if (!_DeserializeInConnectionThread)
            {
                try
                {
                    while (_Connection != null && _Connection.IsAlive)
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
            else
            {
                PendingRead pending = default(PendingRead);
                var queue = _PendingReadQueue;
                if (queue != null)
                {
                    while (!queue.TryDequeue(out pending))
                    {
                        _WaitForObjRead.WaitOne(CONST.MAX_WAIT_MILLISECONDS);
                        queue = _PendingReadQueue;
                        if (queue == null)
                        {
                            break;
                        }
                        if (_Connection == null || !_Connection.IsAlive)
                        {
                            break;
                        }
                    }
                }
                seq = pending.Seq;
                sseq = pending.SSeq;
                type = pending.Type;
                return pending.Obj;
            }
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
            if (!IsServer)
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
#endregion

#region Write
        public uint Write(object obj)
        {
            return Write(obj, 0);
        }
        public uint Write(object obj, uint seq_pingback)
        {
            return Write(obj, seq_pingback, 0);
        }
        public uint Write(object obj, uint seq_pingback, uint flags)
        {
            // seq
            uint seq = 0, sseq = 0;
            uint thisseq;
            if (IsServer)
            {
                seq = seq_pingback;
                sseq = thisseq = (uint)Interlocked.Increment(ref _NextSeq) - 1;
            }
            else
            {
                seq = thisseq = (uint)Interlocked.Increment(ref _NextSeq) - 1;
                sseq = seq_pingback;
            }
            if (!_SerializeInConnectionThread)
            {
                // type
                var rw = _SerConfig.ReaderWriter;
                var type = rw.GetDataType(obj);
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
                PlatDependant.LogError(Environment.TickCount.ToString() + $" Write(type{type} seq{seq} sseq{sseq})");
#endif
                // write obj
                var stream = rw.Write(obj);
                if (stream != null)
                {
                    // post process (encrypt etc.)
                    var processors = _SerConfig.PostProcessors;
                    for (int i = 0; i < processors.Count; ++i)
                    {
                        var processor = processors[i];
                        flags = processor.Process(stream, 0, flags, type, seq, sseq, IsServer);
                    }
                    // compose block
                    _SerConfig.Composer.PrepareBlock(stream, type, flags, seq, sseq);
                    // send
                    _Stream.Write(stream, 0, stream.Count);
                }
            }
            else
            { // if we directly send the obj to the connection thread, we need to clone it. So it seems to be better to serialize it here.
                _Stream.Write(new PendingWrite() { Obj = obj, Seq = seq, SSeq = sseq, Flags = flags }, _SendSerializer);
            }
            return thisseq;
        }

        protected readonly SendSerializer _SendSerializer;
        protected class PendingWrite
        {
            public object Obj;
            public uint Seq;
            public uint SSeq;
            public uint Flags;
        }
        public ValueList<PooledBufferSpan> SerializeMessage(object obj)
        {
            ValueList<PooledBufferSpan> rv = new ValueList<PooledBufferSpan>();
            var mess = obj as PendingWrite;
            if (mess != null)
            {
                // type
                var rw = _SerConfig.ReaderWriter;
                var type = rw.GetDataType(mess.Obj);
                // seq
                uint seq = mess.Seq, sseq = mess.SSeq;
                // write obj
                var stream = rw.Write(mess.Obj);
                if (stream != null)
                {
                    // post process (encrypt etc.)
                    var flags = mess.Flags;
                    var processors = _SerConfig.PostProcessors;
                    for (int i = 0; i < processors.Count; ++i)
                    {
                        var processor = processors[i];
                        flags = processor.Process(stream, 0, flags, type, seq, sseq, IsServer);
                    }
                    // compose block
                    _SerConfig.Composer.PrepareBlock(stream, type, flags, seq, sseq);
                    // send
                    stream.Seek(0, SeekOrigin.Begin);
                    var count = stream.Count;
                    int cntwrote = 0;
                    while (cntwrote < count)
                    {
                        var pbuffer = BufferPool.GetBufferFromPool();
                        var sbuffer = pbuffer.Buffer;
                        int scnt = count - cntwrote;
                        if (sbuffer.Length < scnt)
                        {
                            scnt = sbuffer.Length;
                        }
                        stream.Read(sbuffer, 0, scnt);
                        rv.Add(new PooledBufferSpan() { WholeBuffer = pbuffer, Length = scnt });
                        cntwrote += scnt;
                    }
                }
            }
            return rv;
        }
#endregion

        public event Action OnClose = () => { };
#region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
                _Stream.Dispose();
                if (!LeaveOpen)
                {
                    var dispcon = _Connection as IDisposable;
                    if (dispcon != null)
                    {
                        dispcon.Dispose();
                    }
                }
                _Connection = null;
                _ServerConnection = null;
                _PositiveConnection = null;
                _Stream = null;
                if (_Splitter != null)
                {
                    _Splitter.OnReceiveBlock -= ReceiveBlock;
                    _Splitter.Dispose();
                    _Splitter = null;
                }
                _SerConfig = null;
                _PendingReadQueue = null;
                _WaitForObjRead.Set();
                OnClose();
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

#region IPositiveConnection
        protected IPositiveConnection _PositiveConnection;
        public bool PositiveMode
        {
            get
            {
                if (_PositiveConnection != null)
                {
                    return _PositiveConnection.PositiveMode;
                }
                return false;
            }
            set
            {
                if (_PositiveConnection != null)
                {
                    _PositiveConnection.PositiveMode = value;
                }
            }
        }
        public void Step()
        {
            if (_PositiveConnection != null)
            {
                _PositiveConnection.Step();
            }
        }
#endregion
    }

    public class ObjServer : IPositiveConnection, IChannel, IDisposable
    {
        protected IPersistentConnectionServer _Server;
        protected SerializationConfig _SerConfig;
        protected IDictionary<string, object> _ExtraConfig;

        public ObjServer(IPersistentConnectionServer raw, SerializationConfig sconfig, IDictionary<string, object> exconfig)
        {
            _ExtraConfig = exconfig;
            _SerConfig = sconfig;
            _Server = raw;
            if (raw is IPersistentConnection)
            {
                var connection = raw as IPersistentConnection;
                connection.OnUpdate = OnConnectionUpdate;
            }
            _PositiveConnection = raw as IPositiveConnection;
        }
        public ObjServer(IPersistentConnectionServer raw, SerializationConfig sconfig)
            : this(raw, sconfig, null)
        { }
        public ObjServer(IPersistentConnectionServer raw)
            : this(raw, SerializationConfig.Default, null)
        { }

        protected bool _Started = false;
        public void Start()
        {
            if (!_Started)
            {
                _Server.StartConnect();
                _Started = true;
            }
        }
        public bool IsStarted { get { return _Started; } }
        public bool IsAlive { get { return _Server.IsAlive; } }

        public ObjClient GetConnection()
        {
            var raw = _Server.PrepareConnection();
            var client = new ObjClient(raw, _SerConfig, _ExtraConfig) { _IsServer = true };
            client.OnConnected += FireOnConnected;
            client.Start();
            return client;
        }
        public event Action<IChannel> OnConnected;
        protected void FireOnConnected(IChannel child)
        {
            child.OnConnected -= FireOnConnected;
            if (OnConnected != null)
            {
                OnConnected(child);
            }
        }

        protected int OnConnectionUpdate(IPersistentConnection connection)
        {
            if (OnUpdate != null)
            {
                OnUpdate();
            }
            return int.MinValue;
        }
        public event Action OnUpdate;

        public event Action OnClose = () => { };
#region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
                if (_Server is IDisposable)
                {
                    ((IDisposable)_Server).Dispose();
                }
                _Server = null;
                _SerConfig = null;
                OnClose();
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

#region IPositiveConnection
        protected IPositiveConnection _PositiveConnection;
        public bool PositiveMode
        {
            get
            {
                if (_PositiveConnection != null)
                {
                    return _PositiveConnection.PositiveMode;
                }
                return false;
            }
            set
            {
                if (_PositiveConnection != null)
                {
                    _PositiveConnection.PositiveMode = value;
                }
            }
        }
        public void Step()
        {
            if (_PositiveConnection != null)
            {
                _PositiveConnection.Step();
            }
        }
#endregion
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;
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
    public class KCPServer : IPersistentConnectionServer, IPositiveConnection, IDisposable
    {
        public class ServerConnection : IPersistentConnection, IServerConnection, IDisposable
        {
            protected uint _Conv;
            private class KCPServerConnectionInfo
            {
                public KCPServer Server;
                public IPEndPoint EP;
            }
            private KCPServerConnectionInfo _Info = new KCPServerConnectionInfo();
            protected GCHandle _InfoHandle;
            protected bool _Ready = false;
            private bool _Started = false;
            protected bool _Connected = false;

            protected internal ServerConnection(KCPServer server)
            {
                Server = server;
                _InfoHandle = GCHandle.Alloc(_Info);
            }
            public void SetConv(uint conv)
            {
                if (_Ready)
                {
                    PlatDependant.LogError("Can not change conv. Please create another one.");
                }
                else
                {
                    _Conv = conv;
                    _KCP = KCPLib.CreateConnection(conv, (IntPtr)_InfoHandle);
                    _Ready = true;

                    _KCP.SetOutput(Func_KCPOutput);
                    _KCP.NoDelay(1, 10, 2, 1);
                    // set minrto to 10?
                }
            }
            public uint Conv { get { return _Conv; } }

            public KCPServer Server
            {
                get { return _Info.Server; }
                protected set { _Info.Server = value; }
            }
            public IPEndPoint EP
            {
                get { return _Info.EP; }
                protected set { _Info.EP = new IPEndPoint(value.Address, value.Port); }
            }
            public EndPoint RemoteEndPoint
            {
                get { return EP; }
            }
            protected internal KCPLib.Connection _KCP;
            private bool _Disposed = false;

            internal void DestroySelf(bool inFinalizer)
            {
                if (!_Disposed)
                {
                    _Disposed = true;
                    if (_Ready)
                    {
                        _KCP.Release();
                    }
                    _InfoHandle.Free();
                    //_Info = null; // maybe we shoud not release this info.

                    // set handlers to null.
                    _OnUpdate = null;
                    _OnReceive = null;
                    //_OnSendComplete = null;
                }
                if (!inFinalizer)
                {
                    GC.SuppressFinalize(this);
                }
            }
            public void Dispose()
            {
                Dispose(false);
            }
            public void Dispose(bool inFinalizer)
            {
                if (!_Disposed)
                {
                    Server.RemoveConnection(this);
                    DestroySelf(inFinalizer);
                }
            }
            ~ServerConnection()
            {
                Dispose(true);
            }

            protected static KCPLib.kcp_output Func_KCPOutput = new KCPLib.kcp_output(KCPOutput);
            [AOT.MonoPInvokeCallback(typeof(KCPLib.kcp_output))]
            private static int KCPOutput(IntPtr buf, int len, KCPLib.Connection kcp, IntPtr user)
            {
                try
                {
                    var gchandle = (GCHandle)user;
                    var info = gchandle.Target as KCPServerConnectionInfo;
                    if (info != null && info.EP != null)
                    {
                        var binfo = BufferPool.GetBufferFromPool(len);
                        Marshal.Copy(buf, binfo.Buffer, 0, len);
                        info.Server._Connection.SendRaw(binfo, len, info.EP
                            //, success => BufferPool.ReturnRawBufferToPool(buffer)
                            );
                        binfo.Release();
                    }
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
                return 0;
            }

            protected int _ConnectionThreadID;
            protected byte[] _RecvBuffer = new byte[CONST.MTU];
            protected void DoSendWork(MessageInfo minfo)
            {
                ValueList<PooledBufferSpan> messages;
                if (minfo.Serializer != null)
                {
                    messages = minfo.Serializer(minfo.Raw);
                }
                else
                {
                    messages = minfo.Buffers;
                }
                for (int i = 0; i < messages.Count; ++i)
                {
                    var message = messages[i];
                    var cnt = message.Length;
                    if (cnt > CONST.MTU)
                    {
                        int offset = 0;
                        var pinfo = BufferPool.GetBufferFromPool();
                        var buffer = pinfo.Buffer;
                        while (cnt > CONST.MTU)
                        {
                            Buffer.BlockCopy(message.Buffer, offset, buffer, 0, CONST.MTU);
                            _KCP.Send(buffer, CONST.MTU);
                            cnt -= CONST.MTU;
                            offset += CONST.MTU;
                        }
                        if (cnt > 0)
                        {
                            Buffer.BlockCopy(message.Buffer, offset, buffer, 0, cnt);
                            _KCP.Send(buffer, cnt);
                        }
                        pinfo.Release();
                    }
                    else
                    {
                        _KCP.Send(message.Buffer, cnt);
                    }
                    message.Release();
                }
                //if (_OnSendComplete != null)
                //{
                //    _OnSendComplete(message, true);
                //}
            }
            protected internal virtual int Update()
            {
                if (_ConnectionThreadID == 0)
                {
                    _ConnectionThreadID = Thread.CurrentThread.ManagedThreadId;
                }
                if (!_Ready)
                {
                    return int.MinValue;
                }
                // 1, send.
                if (_Started)
                {
                    MessageInfo minfo;
                    while (_PendingSendMessages.TryDequeue(out minfo))
                    {
                        DoSendWork(minfo);
                    }
                }
                // 2, real update.
                _KCP.Update((uint)Environment.TickCount);
                // 3, receive
                if (_Started)
                {
                    int recvcnt;
                    while ((recvcnt = _KCP.Receive(_RecvBuffer, CONST.MTU)) > 0)
                    {
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.Append(Environment.TickCount);
                            sb.Append(" KCP Server Recv ");
                            sb.Append(recvcnt);
                            for (int i = 0; i < recvcnt; ++i)
                            {
                                if (i % 32 == 0)
                                {
                                    sb.AppendLine();
                                }
                                sb.Append(_RecvBuffer[i].ToString("X2"));
                                sb.Append(" ");
                            }
                            PlatDependant.LogInfo(sb);
                        }
#endif
                        if (_OnReceive != null)
                        {
                            _OnReceive(_RecvBuffer, recvcnt, _Info.EP);
                        }
                    }
                }
                if (_OnUpdate != null)
                {
                    return _OnUpdate(this);
                }
                else
                {
                    return int.MinValue;
                }
            }
            protected internal virtual bool Feed(byte[] data, int cnt, IPEndPoint ep)
            {
                if (_ConnectionThreadID == 0)
                {
                    _ConnectionThreadID = Thread.CurrentThread.ManagedThreadId;
                }
                if (_Ready)
                {
                    if (_KCP.Input(data, cnt) == 0)
                    {
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.Append(Environment.TickCount);
                            sb.Append(" KCP Server Feed ");
                            sb.Append(cnt);
                            for (int i = 0; i < cnt; ++i)
                            {
                                if (i % 32 == 0)
                                {
                                    sb.AppendLine();
                                }
                                sb.Append(data[i].ToString("X2"));
                                sb.Append(" ");
                            }
                            PlatDependant.LogInfo(sb);
                        }
#endif
                        if (!ep.Equals(EP))
                        {
                            EP = ep;
                        }
                        if (!_Connected)
                        {
                            _Connected = true;
                            FireOnConnected();
                        }
                        return true;
                    }
                }
                return false;
            }

            public void StartConnect()
            {
                _Started = true;
            }
            public bool IsAlive
            {
                get
                {
                    try
                    {
                        return !_Disposed && Server.IsAlive;
                    }
                    catch
                    {
                        // this means the connection is closed.
                        return false;
                    }
                }
            }
            public bool IsStarted
            {
                get
                {
                    return _Started || !IsAlive;
                }
            }
            public bool IsConnected
            {
                get { return _Connected; }
            }
            public event Action OnConnected;
            protected void FireOnConnected()
            {
                if (OnConnected != null)
                {
                    OnConnected();
                }
            }
            protected ReceiveHandler _OnReceive;
            /// <summary>
            /// This will be called in connection thread.
            /// </summary>
            public ReceiveHandler OnReceive
            {
                get { return _OnReceive; }
                set
                {
                    if (value != _OnReceive)
                    {
                        if (IsStarted)
                        {
                            PlatDependant.LogError("Cannot change OnReceive when connection started");
                        }
                        else
                        {
                            _OnReceive = value;
                        }
                    }
                }
            }
            protected UpdateHandler _OnUpdate;
            /// <summary>
            /// This will be called in connection thread.
            /// </summary>
            public UpdateHandler OnUpdate
            {
                get { return _OnUpdate; }
                set
                {
                    if (value != _OnUpdate)
                    {
                        if (IsStarted)
                        {
                            PlatDependant.LogError("Cannot change OnUpdate when connection started");
                        }
                        else
                        {
                            _OnUpdate = value;
                        }
                    }
                }
            }
            //protected SendCompleteHandler _OnSendComplete;
            ///// <summary>
            ///// This will be called in undetermined thread.
            ///// </summary>
            //public SendCompleteHandler OnSendComplete
            //{
            //    get { return _OnSendComplete; }
            //    set
            //    {
            //        if (value != _OnSendComplete)
            //        {
            //            if (IsConnectionAlive)
            //            {
            //                PlatDependant.LogError("Cannot change OnSendComplete when connection started");
            //            }
            //            else
            //            {
            //                _OnSendComplete = value;
            //            }
            //        }
            //    }
            //}

            protected ConcurrentQueueGrowOnly<MessageInfo> _PendingSendMessages = new ConcurrentQueueGrowOnly<MessageInfo>();
            public virtual bool TrySend(MessageInfo minfo)
            {
                if (_Ready && _Started && Thread.CurrentThread.ManagedThreadId == _ConnectionThreadID)
                {
                    DoSendWork(minfo);
                    return true;
                }
                else
                {
                    _PendingSendMessages.Enqueue(minfo);
                    return Server._Connection.TrySend(new MessageInfo());
                }
            }
            public void Send(IPooledBuffer data, int cnt)
            {
                TrySend(new MessageInfo(data, cnt));
            }
            public void Send(ValueList<PooledBufferSpan> data)
            {
                TrySend(new MessageInfo(data));
            }
            public void Send(object raw, SendSerializer serializer)
            {
                TrySend(new MessageInfo(raw, serializer));
            }
            public void Send(byte[] data, int cnt)
            {
                Send(new UnpooledBuffer(data), cnt);
            }
            public void Send(byte[] data)
            {
                Send(data, data.Length);
            }
        }

        internal UDPServer _Connection;
        private GCHandle _ConnectionHandle;
        protected bool _Disposed = false;

        protected List<ServerConnection> _Connections = new List<ServerConnection>();

        public KCPServer(int port)
        {
            _Connection = new UDPServer(port);
            _ConnectionHandle = GCHandle.Alloc(_Connection);

            _Connection.UpdateInterval = 10;
            _Connection.PreDispose = _con => DisposeSelf();
            _Connection.OnReceive = (data, cnt, sender) =>
            {
                ServerConnection[] cons;
                lock (_Connections)
                {
                    cons = _Connections.ToArray();
                }
                for (int i = 0; i < cons.Length; ++i)
                {
                    var con = cons[i];
                    if (con.Feed(data, cnt, sender as IPEndPoint))
                    {
                        return;
                    }
                }
            };
            _Connection.OnUpdate = _con =>
            {
                ServerConnection[] cons;
                lock (_Connections)
                {
                    cons = _Connections.ToArray();
                }
                int waitinterval = int.MaxValue;
                for (int i = 0; i < cons.Length; ++i)
                {
                    var con = cons[i];
                    var interval = con.Update();
                    if (interval >= 0 && interval < waitinterval)
                    {
                        waitinterval = interval;
                    }
                }
                if (waitinterval == int.MaxValue)
                {
                    return int.MinValue;
                }
                else
                {
                    return waitinterval;
                }
            };
        }

        public bool IsAlive
        {
            get { return _Connection.IsAlive; }
        }
        public bool IsStarted
        {
            get { return _Connection.IsStarted; }
        }
        public void StartConnect()
        {
            _Connection.StartConnect();
        }

        public bool PositiveMode
        {
            get { return _Connection.PositiveMode; }
            set { _Connection.PositiveMode = value; }
        }
        public void Step()
        {
            _Connection.Step();
        }

        public virtual ServerConnection PrepareConnection()
        {
            var con = new ServerConnection(this);
            Action onChildConnected = null;
            onChildConnected = () =>
            {
                con.OnConnected -= onChildConnected;
                FireOnConnected(con);
            };
            con.OnConnected += onChildConnected;
            lock (_Connections)
            {
                _Connections.Add(con);
            }
            return con;
        }
        protected void FireOnConnected(IServerConnection child)
        {
            if (OnConnected != null)
            {
                OnConnected(child);
            }
        }
        public event ConnectedToServerHandler OnConnected;

        IServerConnection IPersistentConnectionServer.PrepareConnection()
        {
            return PrepareConnection();
        }
        internal void RemoveConnection(IPersistentConnection con)
        {
            int index = -1;
            lock (_Connections)
            {
                for (int i = 0; i < _Connections.Count; ++i)
                {
                    if (_Connections[i] == con)
                    {
                        index = i;
                        break;
                    }
                }
                if (index >= 0)
                {
                    _Connections.RemoveAt(index);
                }
            }
        }
        protected virtual void DisposeSelf()
        {
            if (!_Disposed)
            {
                _Disposed = true;
                _ConnectionHandle.Free();
                lock (_Connections)
                {
                    for (int i = 0; i < _Connections.Count; ++i)
                    {
                        _Connections[i].DestroySelf(false);
                    }
                    _Connections.Clear();
                }
            }
        }
        public void Dispose()
        {
            Dispose(false);
        }
        public void Dispose(bool inFinalizer)
        {
            _Connection.Dispose(inFinalizer);
        }
        ~KCPServer()
        {
            Dispose(true);
        }
    }

    public static partial class ConnectionFactory
    {
        private static RegisteredCreator _Reg_KCPRaw = new RegisteredCreator("kcpraw"
            , uri => new KCPClient(uri.ToString())
            , uri =>
            {
                var port = uri.Port;
                return new KCPServer(port);
            });
    }
}

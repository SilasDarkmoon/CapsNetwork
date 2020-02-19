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
    public class KCPServer : IPersistentConnectionServer, IDisposable
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
                    _KCP = KCPLib.kcp_create(conv, (IntPtr)_InfoHandle);
                    _Ready = true;

                    _KCP.kcp_setoutput(Func_KCPOutput);
                    _KCP.kcp_nodelay(1, 10, 2, 1);
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
                        _KCP.kcp_release();
                    }
                    _InfoHandle.Free();
                    _Info = null;

                    // set handlers to null.
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

            protected byte[] _RecvBuffer = new byte[CONST.MTU];
            protected internal virtual void Update()
            {
                if (!_Ready)
                {
                    return;
                }
                // 1, send.
                if (_Started)
                {
                    MessageInfo minfo;
                    while (_PendingSendMessages.TryDequeue(out minfo))
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
                                    _KCP.kcp_send(buffer, CONST.MTU);
                                    cnt -= CONST.MTU;
                                    offset += CONST.MTU;
                                }
                                if (cnt > 0)
                                {
                                    Buffer.BlockCopy(message.Buffer, offset, buffer, 0, cnt);
                                    _KCP.kcp_send(buffer, cnt);
                                }
                                pinfo.Release();
                            }
                            else
                            {
                                _KCP.kcp_send(message.Buffer, cnt);
                            }
                            message.Release();
                        }
                        //if (_OnSendComplete != null)
                        //{
                        //    _OnSendComplete(message, true);
                        //}
                    }
                }
                // 2, real update.
                _KCP.kcp_update((uint)Environment.TickCount);
                // 3, receive
                if (_Started)
                {
                    int recvcnt = _KCP.kcp_recv(_RecvBuffer, CONST.MTU);
                    if (_OnReceive != null)
                    {
                        if (recvcnt > 0)
                        {
                            _OnReceive(_RecvBuffer, recvcnt, _Info.EP);
                        }
                    }
                }
            }
            protected internal virtual bool Feed(byte[] data, int cnt, IPEndPoint ep)
            {
                if (_Ready)
                {
                    if (_KCP.kcp_input(data, cnt) == 0)
                    {
                        if (!ep.Equals(EP))
                        {
                            EP = ep;
                        }
                        if (!_Connected)
                        {
                            _Connected = true;
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
            public bool IsConnectionAlive
            {
                get
                {
                    try
                    {
                        return _Started && Server._Connection.IsConnectionAlive;
                    }
                    catch
                    {
                        // this means the connection is closed.
                        return false;
                    }
                }
            }
            public bool IsConnected
            {
                get { return _Connected; }
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
                        if (IsConnectionAlive)
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
                _PendingSendMessages.Enqueue(minfo);
                return Server._Connection.TrySend(new MessageInfo());
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
                lock (_Connections)
                {
                    for (int i = 0; i < _Connections.Count; ++i)
                    {
                        var con = _Connections[i];
                        if (con.Feed(data, cnt, sender as IPEndPoint))
                        {
                            return;
                        }
                    }
                }
            };
            _Connection.OnUpdate = _con =>
            {
                lock (_Connections)
                {
                    for (int i = 0; i < _Connections.Count; ++i)
                    {
                        var con = _Connections[i];
                        con.Update();
                    }
                }
            };
        }

        public bool IsAlive
        {
            get { return _Connection.IsConnectionAlive; }
        }
        public void StartListening()
        {
            _Connection.StartConnect();
        }
        public virtual ServerConnection PrepareConnection()
        {
            var con = new ServerConnection(this);
            lock (_Connections)
            {
                _Connections.Add(con);
            }
            return con;
        }
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

    public static partial class PersistentConnectionFactory
    {
        private static RegisteredCreator _Reg_KCPRaw = new RegisteredCreator("kcpraw"
            , url => new KCPClient(url)
            , url =>
            {
                var uri = new Uri(url);
                var port = uri.Port;
                return new KCPServer(port);
            });
    }
}

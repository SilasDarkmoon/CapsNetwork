using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;

using PlatDependant = Capstones.UnityEngineEx.PlatDependant;
using TaskProgress = Capstones.UnityEngineEx.TaskProgress;

namespace Capstones.Net
{
    public class TCPServer : TCPClient, IPersistentConnectionServer
    {
        public class ServerConnection : TCPClient, IServerConnection
        {
            protected TCPServer _Server;
            protected bool _Connected = false;

            internal ServerConnection(TCPServer server)
            {
                _Server = server;
            }

            protected override void PrepareSocket()
            {
                while ((_Socket = _Server.TryAccept(this)) == null)
                {
                    if (_ConnectWorkFinished)
                    {
                        return;
                    }
                }
                _Connected = true;
                if (OnConnected != null)
                {
                    OnConnected(this);
                }
            }
            public event Action<IServerConnectionLifetime> OnConnected;
            public bool IsConnected
            {
                get { return _Connected; }
            }
        }

        public TCPServer(int port)
        {
            _Port = port;
        }

        protected int _Port;
        public int Port
        {
            get { return _Port; }
            set
            {
                if (value != _Port)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change port when server started");
                    }
                    else
                    {
                        _Port = value;
                    }
                }
            }
        }

        protected Socket _Socket6;

        protected struct AcceptedSocketInfo
        {
            public Socket Socket;
            public bool IsIPv6;
        }
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        protected ConcurrentQueueGrowOnly<AcceptedSocketInfo> _AcceptedSockets = new ConcurrentQueueGrowOnly<AcceptedSocketInfo>();
#else
        protected System.Collections.Concurrent.ConcurrentQueue<AcceptedSocketInfo> _AcceptedSockets = new System.Collections.Concurrent.ConcurrentQueue<AcceptedSocketInfo>();
#endif
        protected Semaphore _AcceptedSemaphore = new Semaphore(0, CONST.MAX_SERVER_PENDING_CONNECTIONS * 2);
        protected int _NeedAcceptSocket4Count = CONST.MAX_SERVER_PENDING_CONNECTIONS;
        protected int _NeedAcceptSocket6Count = CONST.MAX_SERVER_PENDING_CONNECTIONS;

        protected void BeginAccept4()
        {
            try
            {
                _Socket.BeginAccept(ar =>
                {
                    try
                    {
                        var socket = _Socket.EndAccept(ar);
                        if (socket == null)
                        {
                            throw new NullReferenceException("Accepted a null socket.");
                        }
                        _AcceptedSockets.Enqueue(new AcceptedSocketInfo() { Socket = socket, IsIPv6 = false });
                        _AcceptedSemaphore.Release();
                    }
                    catch (Exception e)
                    {
                        System.Threading.Interlocked.Increment(ref _NeedAcceptSocket4Count);
                        _HaveDataToSend.Set();
                        PlatDependant.LogError(e);
                    }
                }, null);
            }
            catch (Exception e)
            {
                System.Threading.Interlocked.Increment(ref _NeedAcceptSocket4Count);
                _HaveDataToSend.Set();
                PlatDependant.LogError(e);
            }
        }
        protected void BeginAccept6()
        {
            try
            {
                _Socket6.BeginAccept(ar =>
                {
                    try
                    {
                        var socket = _Socket6.EndAccept(ar);
                        if (socket == null)
                        {
                            throw new NullReferenceException("Accepted a null socket.");
                        }
                        _AcceptedSockets.Enqueue(new AcceptedSocketInfo() { Socket = socket, IsIPv6 = true });
                        _AcceptedSemaphore.Release();
                    }
                    catch (Exception e)
                    {
                        System.Threading.Interlocked.Increment(ref _NeedAcceptSocket6Count);
                        _HaveDataToSend.Set();
                        PlatDependant.LogError(e);
                    }
                }, null);
            }
            catch (Exception e)
            {
                System.Threading.Interlocked.Increment(ref _NeedAcceptSocket6Count);
                _HaveDataToSend.Set();
                PlatDependant.LogError(e);
            }
        }
        public int ListeningCount;
        public Socket Accept()
        {
            System.Threading.Interlocked.Increment(ref ListeningCount);
            try
            {
                _AcceptedSemaphore.WaitOne();
                AcceptedSocketInfo info;
                if (_AcceptedSockets.TryDequeue(out info))
                {
                    if (info.IsIPv6)
                    {
                        BeginAccept6();
                    }
                    else
                    {
                        BeginAccept4();
                    }
                    return info.Socket;
                }
                return null;
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref ListeningCount);
            }
        }
        public Socket TryAccept(ServerConnection con)
        {
            System.Threading.Interlocked.Increment(ref ListeningCount);
            try
            {
                bool got = _AcceptedSemaphore.WaitOne(CONST.MAX_WAIT_MILLISECONDS);
                if (!got)
                {
                    return null;
                }
                if (!con.IsAlive)
                {
                    _AcceptedSemaphore.Release();
                    return null;
                }
                AcceptedSocketInfo info;
                if (_AcceptedSockets.TryDequeue(out info))
                {
                    if (info.IsIPv6)
                    {
                        BeginAccept6();
                    }
                    else
                    {
                        BeginAccept4();
                    }
                    return info.Socket;
                }
                return null;
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref ListeningCount);
            }
        }

        protected override IEnumerator ConnectWork()
        {
            try
            {
                try
                {
                    var address4 = IPAddress.Any;
                    _Socket = new Socket(address4.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _Socket.Bind(new IPEndPoint(address4, _Port));
                    _Socket.Listen(CONST.MAX_SERVER_PENDING_CONNECTIONS);

                    var address6 = IPAddress.IPv6Any;
                    _Socket6 = new Socket(address6.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _Socket6.Bind(new IPEndPoint(address6, _Port));
                    _Socket6.Listen(CONST.MAX_SERVER_PENDING_CONNECTIONS);

                    //BeginAccept4();
                    //BeginAccept6();
                }
                catch (ThreadAbortException)
                {
                    if (!_PositiveMode)
                    {
                        Thread.ResetAbort();
                    }
                    yield break;
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                    yield break;
                }
                while (!_ConnectWorkFinished)
                {
                    SpinWait spin = new SpinWait();
                    int cnt4 = _NeedAcceptSocket4Count;
                    while (System.Threading.Interlocked.CompareExchange(ref _NeedAcceptSocket4Count, 0, cnt4) != cnt4)
                    {
                        spin.SpinOnce();
                        cnt4 = _NeedAcceptSocket4Count;
                    }
                    for (int i = 0; i < cnt4; ++i)
                    {
                        BeginAccept4();
                    }
                    spin.Reset();
                    int cnt6 = _NeedAcceptSocket6Count;
                    while (System.Threading.Interlocked.CompareExchange(ref _NeedAcceptSocket6Count, 0, cnt6) != cnt6)
                    {
                        spin.SpinOnce();
                        cnt6 = _NeedAcceptSocket6Count;
                    }
                    for (int i = 0; i < cnt6; ++i)
                    {
                        BeginAccept6();
                    }

                    int waitinterval;
                    try
                    {
                        waitinterval = int.MinValue;
                        if (_OnUpdate != null)
                        {
                            waitinterval = _OnUpdate(this);
                        }
                        if (waitinterval < 0)
                        {
                            waitinterval = CONST.MAX_WAIT_MILLISECONDS;
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        if (!_PositiveMode)
                        {
                            Thread.ResetAbort();
                        }
                        yield break;
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                        yield break;
                    }
                    if (_HaveDataToSend.WaitOne(0))
                    {
                        continue;
                    }
                    if (_PositiveMode)
                    {
                        yield return null;
                    }
                    else
                    {
                        _HaveDataToSend.WaitOne(waitinterval);
                    }
                }

                //_Socket.Shutdown(SocketShutdown.Both);
                //_Socket6.Shutdown(SocketShutdown.Both);
            }
            finally
            {
                //_ConnectWorkStarted = false;
                //_ConnectWorkCanceled = false;
                if (_PreDispose != null)
                {
                    _PreDispose(this);
                }
                if (_Socket != null)
                {
                    _Socket.Close();
                    _Socket = null;
                }
                if (_Socket6 != null)
                {
                    _Socket6.Close();
                    _Socket6 = null;
                }
                // set handlers to null.
                _OnReceive = null;
                _OnSend = null;
                _OnUpdate = null;
                //_OnSendComplete = null;
                _PreDispose = null;
            }
        }
        public override bool TrySend(MessageInfo minfo)
        {
            _HaveDataToSend.Set();
            Start();
            return false;
        }

        public ServerConnection PrepareConnection()
        {
            var con = new ServerConnection(this);
            con.OnConnected += OnChildConnected;
            return con;
        }
        protected void OnChildConnected(IServerConnectionLifetime child)
        {
            child.OnConnected -= OnChildConnected;
            FireOnConnected(child);
        }
        protected void FireOnConnected(IServerConnectionLifetime child)
        {
            var onConnected = OnConnected;
            if (onConnected != null)
            {
                onConnected(child);
            }
        }
        public event Action<IServerConnectionLifetime> OnConnected;

        IServerConnection IPersistentConnectionServer.PrepareConnection()
        {
            return PrepareConnection();
        }
        public bool IsConnected { get { return IsStarted; } }
    }

    public static partial class ConnectionFactory
    {
        private static RegisteredCreator _Reg_TCP = new RegisteredCreator("tcp"
            , uri => new TCPClient(uri.ToString())
            , uri =>
            {
                var port = uri.Port;
                return new TCPServer(port);
            });
    }
}

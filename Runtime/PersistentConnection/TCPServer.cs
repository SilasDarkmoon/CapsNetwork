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
                _Socket = _Server.Accept();
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

        protected Socket _AcceptedSocket4;
        protected Socket _AcceptedSocket6;
        protected Semaphore _AcceptedSemaphore = new Semaphore(0, 2);
        protected void BeginAccept4()
        {
            _Socket.BeginAccept(ar =>
            {
                var socket = _Socket.EndAccept(ar);
                Interlocked.Exchange(ref _AcceptedSocket4, socket);
                _AcceptedSemaphore.Release();
            }, null);
        }
        protected void BeginAccept6()
        {
            _Socket6.BeginAccept(ar =>
            {
                var socket = _Socket6.EndAccept(ar);
                Interlocked.Exchange(ref _AcceptedSocket6, socket);
                _AcceptedSemaphore.Release();
            }, null);
        }
        public Socket Accept()
        {
            _AcceptedSemaphore.WaitOne();
            Socket rv = null;
            SpinWait spin = new SpinWait();
            while (true)
            {
                rv = _AcceptedSocket4;
                if (Interlocked.CompareExchange(ref _AcceptedSocket4, null, rv) == rv)
                {
                    break;
                }
                spin.SpinOnce();
            }
            if (rv != null)
            {
                BeginAccept4();
                return rv;
            }
            spin.Reset();
            while (true)
            {
                rv = _AcceptedSocket6;
                if (Interlocked.CompareExchange(ref _AcceptedSocket6, null, rv) == rv)
                {
                    break;
                }
                spin.SpinOnce();
            }
            if (rv != null)
            {
                BeginAccept6();
                return rv;
            }
            return null;
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

                    BeginAccept4();
                    BeginAccept6();
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

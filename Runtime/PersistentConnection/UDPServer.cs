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
    public class UDPServer : UDPClient
    {
        public UDPServer(int port)
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
                    if (IsConnectionAlive)
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
        protected bool _ListenBroadcast;
        public bool ListenBroadcast
        {
            get { return _ListenBroadcast; }
            set
            {
                if (value != _ListenBroadcast)
                {
                    if (IsConnectionAlive)
                    {
                        PlatDependant.LogError("Cannot change ListenBroadcast when server started");
                    }
                    else
                    {
                        _ListenBroadcast = value;
                    }
                }
            }
        }

        protected Socket _Socket6;
        protected class BroadcastSocketReceiveInfo
        {
            public Socket LocalSocket;
            public EndPoint RemoteEP;
            public byte[] ReceiveData = new byte[CONST.MTU];
            public int ReceiveCount = 0;
            public IAsyncResult ReceiveResult;
            public UDPServer ParentServer;

            public BroadcastSocketReceiveInfo(UDPServer parent, Socket socket, EndPoint init_remote)
            {
                ParentServer = parent;
                LocalSocket = socket;
                RemoteEP = init_remote;
            }

            public void BeginReceive()
            {
                ReceiveCount = 0;
                ReceiveResult = null;
                try
                {
                    ReceiveResult = LocalSocket.BeginReceiveFrom(ReceiveData, 0, CONST.MTU, SocketFlags.None, ref RemoteEP, ar =>
                    {
                        try
                        {
                            ReceiveCount = LocalSocket.EndReceiveFrom(ar, ref RemoteEP);
                        }
                        catch (Exception e)
                        {
                            if (ParentServer.IsConnectionAlive)
                            {
                                if (e is SocketException && ((SocketException)e).ErrorCode == 10054)
                                {
                                // the remote closed.
                            }
                                else
                                {
                                //ParentServer._ConnectWorkCanceled = true;
                                PlatDependant.LogError(e);
                                }
                            }
                            return;
                        }
                        ParentServer._HaveDataToSend.Set();
                    }, null);
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
            }
        }
        protected List<BroadcastSocketReceiveInfo> _SocketsBroadcast;
        protected struct KnownRemote
        {
            public IPAddress Address;
            public Socket LocalSocket;
            public int LastTick;
        }
        protected class KnownRemotes
        {
            public Dictionary<IPAddress, KnownRemote> Remotes = new Dictionary<IPAddress, KnownRemote>();
            public int Version;
        }
        protected KnownRemotes _KnownRemotes;
        protected KnownRemotes _KnownRemotesR;
        protected KnownRemotes _KnownRemotesS;

        protected override void ConnectWork()
        {
            try
            {
                KnownRemotes remotes = null;
                if (_ListenBroadcast)
                {
                    IPAddressInfo.Refresh();
                    _SocketsBroadcast = new List<BroadcastSocketReceiveInfo>();
                    remotes = new KnownRemotes();
                    _KnownRemotes = new KnownRemotes();
                    _KnownRemotesR = new KnownRemotes();
                    _KnownRemotesS = new KnownRemotes();
                }

                if (_ListenBroadcast)
                {
                    var ipv4addrs = IPAddressInfo.LocalIPv4Addresses;
                    for (int i = 0; i < ipv4addrs.Length; ++i)
                    {
                        try
                        {
                            var address = ipv4addrs[i];
                            var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                            socket.Bind(new IPEndPoint(address, _Port));
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddressInfo.IPv4MulticastAddress, address));
                            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 5);
                            _SocketsBroadcast.Add(new BroadcastSocketReceiveInfo(this, socket, new IPEndPoint(IPAddress.Any, _Port)));
                            if (_Socket == null)
                            {
                                _Socket = socket;
                            }
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(ipv4addrs[i]);
                            PlatDependant.LogError(e);
                        }
                    }
                }
                if (_Socket == null)
                {
                    var address4 = IPAddress.Any;
                    _Socket = new Socket(address4.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                    _Socket.Bind(new IPEndPoint(address4, _Port));
                }

#if NET_STANDARD_2_0 || NET_4_6 || !UNITY_ENGINE && !UNITY_5_3_OR_NEWER
                // Notice: it is a pitty that unity does not support ipv6 multicast. (Unity 5.6)
                if (_ListenBroadcast)
                {
                    var ipv6addrs = IPAddressInfo.LocalIPv6Addresses;
                    for (int i = 0; i < ipv6addrs.Length; ++i)
                    {
                        try
                        {
                            var address = ipv6addrs[i];
                            var maddr = IPAddressInfo.IPv6MulticastAddressOrganization;
                            if (address.IsIPv6SiteLocal)
                            {
                                maddr = IPAddressInfo.IPv6MulticastAddressSiteLocal;
                            }
                            else if (address.IsIPv6LinkLocal)
                            {
                                maddr = IPAddressInfo.IPv6MulticastAddressLinkLocal;
                            }
                            var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                            socket.Bind(new IPEndPoint(address, _Port));
                            var iindex = IPAddressInfo.GetInterfaceIndex(address);
                            if (iindex == 0)
                            {
                                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(maddr));
                            }
                            else
                            {
                                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(maddr, iindex));
                            }
                            socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, 5);
                            _SocketsBroadcast.Add(new BroadcastSocketReceiveInfo(this, socket, new IPEndPoint(IPAddress.IPv6Any, _Port)));
                            if (_Socket6 == null)
                            {
                                _Socket6 = socket;
                            }
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(ipv6addrs[i]);
                            PlatDependant.LogError(e);
                        }
                    }
                }
#endif
                if (_Socket6 == null)
                {
                    var address6 = IPAddress.IPv6Any;
                    _Socket6 = new Socket(address6.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                    _Socket6.Bind(new IPEndPoint(address6, _Port));
                }

                if (_ListenBroadcast)
                {
                    for (int i = 0; i < _SocketsBroadcast.Count; ++i)
                    {
                        var bsinfo = _SocketsBroadcast[i];
                        bsinfo.BeginReceive();
                    }
                    int knownRemotesVersion = 0;
                    while (!_ConnectWorkCanceled)
                    {
                        bool knownRemotesChanged = false;
                        var curTick = Environment.TickCount;
                        for (int i = 0; i < _SocketsBroadcast.Count; ++i)
                        {
                            var bsinfo = _SocketsBroadcast[i];
                            if (bsinfo.ReceiveCount > 0)
                            {
                                var ep = bsinfo.RemoteEP as IPEndPoint;
                                //var remote = new IPEndPoint(ep.Address, ep.Port);
                                remotes.Remotes[ep.Address] = new KnownRemote() { Address = ep.Address, LocalSocket = bsinfo.LocalSocket, LastTick = curTick };
                                knownRemotesChanged = true;
                            }
                        }
                        if (remotes.Remotes.Count > 100)
                        {
                            KnownRemote[] aremotes = new KnownRemote[remotes.Remotes.Count];
                            remotes.Remotes.Values.CopyTo(aremotes, 0);
                            Array.Sort(aremotes, (ra, rb) => ra.LastTick - rb.LastTick);
                            for (int i = 0; i < aremotes.Length - 100; ++i)
                            {
                                var remote = aremotes[i];
                                if (remote.LastTick + 15000 <= curTick)
                                {
                                    remotes.Remotes.Remove(remote.Address);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        // TODO: check dead knownRemotes...
                        if (knownRemotesChanged)
                        {
                            _KnownRemotesR.Remotes.Clear();
                            foreach (var kvp in remotes.Remotes)
                            {
                                _KnownRemotesR.Remotes[kvp.Key] = kvp.Value;
                            }
                            _KnownRemotesR.Version = ++knownRemotesVersion;
                            _KnownRemotesR = System.Threading.Interlocked.Exchange(ref _KnownRemotes, _KnownRemotesR);
                        }

                        if (_OnReceive != null)
                        {
                            for (int i = 0; i < _SocketsBroadcast.Count; ++i)
                            {
                                var bsinfo = _SocketsBroadcast[i];
                                if (bsinfo.ReceiveCount > 0)
                                {
                                    _OnReceive(bsinfo.ReceiveData, bsinfo.ReceiveCount, bsinfo.RemoteEP);
                                }
                            }
                        }
                        for (int i = 0; i < _SocketsBroadcast.Count; ++i)
                        {
                            var bsinfo = _SocketsBroadcast[i];
                            if (bsinfo.ReceiveResult == null || bsinfo.ReceiveResult.IsCompleted)
                            {
                                bsinfo.BeginReceive();
                            }
                        }

                        if (_OnUpdate != null)
                        {
                            _OnUpdate(this);
                        }

                        _HaveDataToSend.WaitOne(_UpdateInterval);
                    }
                }
                else
                {
                    EndPoint sender4 = new IPEndPoint(IPAddress.Any, _Port);
                    EndPoint sender6 = new IPEndPoint(IPAddress.IPv6Any, _Port);

                    byte[] data4 = new byte[CONST.MTU];
                    byte[] data6 = new byte[CONST.MTU];
                    int dcnt4 = 0;
                    int dcnt6 = 0;
                    IAsyncResult readar4 = null;
                    IAsyncResult readar6 = null;

                    Action BeginReceive4 = () =>
                    {
                        try
                        {
                            readar4 = null;
                            readar4 = _Socket.BeginReceiveFrom(data4, 0, CONST.MTU, SocketFlags.None, ref sender4, ar =>
                            {
                                try
                                {
                                    dcnt4 = _Socket.EndReceiveFrom(ar, ref sender4);
                                }
                                catch (Exception e)
                                {
                                    if (IsConnectionAlive)
                                    {
                                        if (e is SocketException && ((SocketException)e).ErrorCode == 10054)
                                        {
                                        // the remote closed.
                                    }
                                        else
                                        {
                                        //_ConnectWorkCanceled = true;
                                        PlatDependant.LogError(e);
                                        }
                                    }
                                    return;
                                }
                                _HaveDataToSend.Set();
                            }, null);
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(e);
                        }
                    };
                    Action BeginReceive6 = () =>
                    {
                        try
                        {
                            readar6 = null;
                            readar6 = _Socket6.BeginReceiveFrom(data6, 0, CONST.MTU, SocketFlags.None, ref sender6, ar =>
                            {
                                try
                                {
                                    dcnt6 = _Socket6.EndReceiveFrom(ar, ref sender6);
                                }
                                catch (Exception e)
                                {
                                    if (IsConnectionAlive)
                                    {
                                        if (e is SocketException && ((SocketException)e).ErrorCode == 10054)
                                        {
                                        // the remote closed.
                                    }
                                        else
                                        {
                                        //_ConnectWorkCanceled = true;
                                        PlatDependant.LogError(e);
                                        }
                                    }
                                    return;
                                }
                                _HaveDataToSend.Set();
                            }, null);
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(e);
                        }
                    };
                    BeginReceive4();
                    BeginReceive6();
                    while (!_ConnectWorkCanceled)
                    {
                        if (_OnReceive != null)
                        {
                            if (dcnt4 > 0)
                            {
                                _OnReceive(data4, dcnt4, sender4);
                                dcnt4 = 0;
                            }
                            if (dcnt6 > 0)
                            {
                                _OnReceive(data6, dcnt6, sender6);
                                dcnt6 = 0;
                            }
                        }
                        if (readar4 == null || readar4.IsCompleted)
                        {
                            BeginReceive4();
                        }
                        if (readar6 == null || readar6.IsCompleted)
                        {
                            BeginReceive6();
                        }

                        if (_OnUpdate != null)
                        {
                            _OnUpdate(this);
                        }

                        _HaveDataToSend.WaitOne(_UpdateInterval);
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
            finally
            {
                _ConnectWorkRunning = false;
                _ConnectWorkCanceled = false;
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
                if (_SocketsBroadcast != null)
                {
                    for (int i = 0; i < _SocketsBroadcast.Count; ++i)
                    {
                        var bsinfo = _SocketsBroadcast[i];
                        if (bsinfo != null && bsinfo.LocalSocket != null)
                        {
                            bsinfo.LocalSocket.Close();
                        }
                    }
                    _SocketsBroadcast = null;
                }
                // set handlers to null.
                _OnReceive = null;
                _OnSend = null;
                //_OnSendComplete = null;
                _OnUpdate = null;
                _PreDispose = null;
            }
        }
        public override bool TrySend(BufferInfo binfo)
        {
            _HaveDataToSend.Set();
            StartConnect();
            return false;
        }

        public void SendRaw(IPooledBuffer data, int cnt, IPEndPoint ep, Action<bool> onComplete)
        {
            data.AddRef();
            if (_ListenBroadcast)
            {
                int curVer = 0;
                if (_KnownRemotesS != null)
                {
                    curVer = _KnownRemotesS.Version;
                }
                int rver = 0;
                if (_KnownRemotes != null)
                {
                    rver = _KnownRemotes.Version;
                }
                if (rver > curVer)
                {
                    _KnownRemotesS = System.Threading.Interlocked.Exchange(ref _KnownRemotes, _KnownRemotesS);
                }
                Socket knowSocket = null;
                if (_KnownRemotesS != null)
                {
                    KnownRemote remote;
                    if (_KnownRemotesS.Remotes.TryGetValue(ep.Address, out remote))
                    {
                        knowSocket = remote.LocalSocket;
                        remote.LastTick = Environment.TickCount;
                        _KnownRemotesS.Remotes[ep.Address] = remote;
                    }
                }
                if (knowSocket != null)
                {
                    try
                    {
                        var info = GetSendAsyncInfoFromPool();
                        info.Data = data;
                        info.Socket = knowSocket;
                        info.OnComplete = onComplete;
                        knowSocket.BeginSendTo(data.Buffer, 0, cnt, SocketFlags.None, ep, info.OnAsyncCallback, null);
                        return;
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                    }
                }
            }
            else
            {
                if (ep.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (_Socket6 != null)
                    {
                        try
                        {
                            var info = GetSendAsyncInfoFromPool();
                            info.Data = data;
                            info.Socket = _Socket6;
                            info.OnComplete = onComplete;
                            _Socket6.BeginSendTo(data.Buffer, 0, cnt, SocketFlags.None, ep, info.OnAsyncCallback, null);
                            return;
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(e);
                        }
                    }
                }
                else
                {
                    if (_Socket != null)
                    {
                        try
                        {
                            var info = GetSendAsyncInfoFromPool();
                            info.Data = data;
                            info.Socket = _Socket;
                            info.OnComplete = onComplete;
                            _Socket.BeginSendTo(data.Buffer, 0, cnt, SocketFlags.None, ep, info.OnAsyncCallback, null);
                            return;
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(e);
                        }
                    }
                }
            }
            if (onComplete != null)
            {
                onComplete(false);
            }
            data.Release();
        }
        //public void SendRaw(byte[] data, int cnt, IPEndPoint ep, Action onComplete)
        //{
        //    SendRaw(data, cnt, ep, onComplete == null ? null : (Action<bool>)(success => onComplete()));
        //}
        public void SendRaw(IPooledBuffer data, int cnt, IPEndPoint ep)
        {
            SendRaw(data, cnt, ep, null);
        }
        public void SendRaw(IPooledBuffer data, IPEndPoint ep)
        {
            SendRaw(data, data.Buffer.Length, ep, null);
        }
        public void SendRaw(byte[] data, int cnt, IPEndPoint ep, Action<bool> onComplete)
        {
            SendRaw(new UnpooledBuffer(data), cnt, ep, onComplete);
        }
        public void SendRaw(byte[] data, int cnt, IPEndPoint ep)
        {
            SendRaw(data, cnt, ep, null);
        }
        public void SendRaw(byte[] data, IPEndPoint ep)
        {
            SendRaw(data, data.Length, ep);
        }
    }
}

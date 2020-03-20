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
        protected bool _ListenBroadcast;
        public bool ListenBroadcast
        {
            get { return _ListenBroadcast; }
            set
            {
                if (value != _ListenBroadcast)
                {
                    if (IsStarted)
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
            protected IPEndPoint BroadcastEP;
            public byte[] ReceiveData = new byte[CONST.MTU];
            public int ReceiveCount = 0;
            public IAsyncResult ReceiveResult;
            public UDPServer ParentServer;
            protected AsyncCallback EndReceiveFunc;
            public ConcurrentQueueGrowOnly<RecvFromInfo> PendingRecvMessages = new ConcurrentQueueGrowOnly<RecvFromInfo>();

            public BroadcastSocketReceiveInfo(UDPServer parent, Socket socket, IPEndPoint init_remote)
            {
                ParentServer = parent;
                LocalSocket = socket;
                BroadcastEP = init_remote;
                RemoteEP = new IPEndPoint(IPAddress.Any, 0);
                EndReceiveFunc = EndReceive;
            }

            protected void EndReceive(IAsyncResult ar)
            {
                try
                {
                    ReceiveCount = LocalSocket.EndReceiveFrom(ar, ref RemoteEP);
                    if (ReceiveCount > 0)
                    {
                        var ep = GetIPEndPointFromPool();
                        ep.Address = ((IPEndPoint)RemoteEP).Address;
                        ep.Port = ((IPEndPoint)RemoteEP).Port;
                        PendingRecvMessages.Enqueue(new RecvFromInfo() { Buffers = BufferPool.GetPooledBufferList(ReceiveData, 0, ReceiveCount), Remote = ep });
                        ParentServer._HaveDataToSend.Set();
                    }
                }
                catch (Exception e)
                {
                    if (ParentServer.IsAlive)
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
                }
                finally
                {
                    if (!ParentServer._ConnectWorkFinished && LocalSocket != null)
                    {
                        BeginReceive();
                    }
                }
            }
            public void BeginReceive()
            {
                while (!ParentServer._ConnectWorkFinished && LocalSocket != null)
                {
                    ReceiveCount = 0;
                    ReceiveResult = null;
                    try
                    {
                        var iep = (IPEndPoint)RemoteEP;
                        iep.Address = BroadcastEP.Address;
                        iep.Port = BroadcastEP.Port;
                        ReceiveResult = LocalSocket.BeginReceiveFrom(ReceiveData, 0, CONST.MTU, SocketFlags.None, ref RemoteEP, EndReceiveFunc, null);
                        return;
                    }
                    catch (SocketException e)
                    {
                        if (e.ErrorCode == 10054)
                        {
                            // 远程主机强迫关闭了一个现有的连接。
                        }
                        else
                        {
                            PlatDependant.LogError(e);
                        }
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                    }
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

        protected byte[] _ReceiveBuffer6 = new byte[CONST.MTU];
        protected EndPoint _RemoteEP6;
        protected void EndReceive4(IAsyncResult ar)
        {
            try
            {
                var receivecnt = _Socket.EndReceiveFrom(ar, ref _RemoteEP);
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
                if (receivecnt > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(Environment.TickCount);
                    sb.Append(" UDPServer Receiving (IPv4) ");
                    sb.Append(receivecnt);
                    //for (int i = 0; i < receivecnt; ++i)
                    //{
                    //    if (i % 32 == 0)
                    //    {
                    //        sb.AppendLine();
                    //    }
                    //    sb.Append(_ReceiveBuffer[i].ToString("X2"));
                    //    sb.Append(" ");
                    //}
                    PlatDependant.LogInfo(sb);
                }
#endif
                if (receivecnt > 0)
                {
                    var ep = GetIPEndPointFromPool();
                    ep.Address = ((IPEndPoint)_RemoteEP).Address;
                    ep.Port = ((IPEndPoint)_RemoteEP).Port;
                    _PendingRecvMessages.Enqueue(new RecvFromInfo() { Buffers = BufferPool.GetPooledBufferList(_ReceiveBuffer, 0, receivecnt), Remote = ep });
                    _HaveDataToSend.Set();
                }
            }
            catch (Exception e)
            {
                if (IsAlive)
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
            }
            finally
            {
                if (!_ConnectWorkFinished && _Socket != null)
                {
                    BeginReceive4();
                }
            }
        }
        protected AsyncCallback EndReceive4Func;
        protected void EndReceive6(IAsyncResult ar)
        {
            try
            {
                var receivecnt = _Socket6.EndReceiveFrom(ar, ref _RemoteEP6);
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
                if (receivecnt > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("UDPServer Receiving (IPv6) ");
                    sb.Append(receivecnt);
                    //for (int i = 0; i < receivecnt; ++i)
                    //{
                    //    if (i % 32 == 0)
                    //    {
                    //        sb.AppendLine();
                    //    }
                    //    sb.Append(_ReceiveBuffer6[i].ToString("X2"));
                    //    sb.Append(" ");
                    //}
                    PlatDependant.LogInfo(sb);
                }
#endif
                if (receivecnt > 0)
                {
                    var ep = GetIPEndPointFromPool();
                    ep.Address = ((IPEndPoint)_RemoteEP6).Address;
                    ep.Port = ((IPEndPoint)_RemoteEP6).Port;
                    _PendingRecvMessages.Enqueue(new RecvFromInfo() { Buffers = BufferPool.GetPooledBufferList(_ReceiveBuffer6, 0, receivecnt), Remote = ep });
                    _HaveDataToSend.Set();
                }
            }
            catch (Exception e)
            {
                if (IsAlive)
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
            }
            finally
            {
                if (!_ConnectWorkFinished && _Socket6 != null)
                {
                    BeginReceive6();
                }
            }
        }
        protected AsyncCallback EndReceive6Func;
        protected void BeginReceive4()
        {
            while (!_ConnectWorkFinished && _Socket != null)
            {
                try
                {
                    ((IPEndPoint)_RemoteEP).Address = IPAddress.Any;
                    ((IPEndPoint)_RemoteEP).Port = _Port;
                    var cb = EndReceive4Func = EndReceive4Func ?? EndReceive4;
                    _Socket.BeginReceiveFrom(_ReceiveBuffer, 0, CONST.MTU, SocketFlags.None, ref _RemoteEP, cb, null);
                    return;
                }
                catch (SocketException e)
                {
                    if (e.ErrorCode == 10054)
                    {
                        // 远程主机强迫关闭了一个现有的连接。
                    }
                    else
                    {
                        PlatDependant.LogError(e);
                    }
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
            }
        }
        protected void BeginReceive6()
        {
            while (!_ConnectWorkFinished && _Socket6 != null)
            {
                try
                {
                    ((IPEndPoint)_RemoteEP6).Address = IPAddress.IPv6Any;
                    ((IPEndPoint)_RemoteEP6).Port = _Port;
                    var cb = EndReceive6Func = EndReceive6Func ?? EndReceive6;
                    _Socket6.BeginReceiveFrom(_ReceiveBuffer6, 0, CONST.MTU, SocketFlags.None, ref _RemoteEP6, cb, null);
                    return;
                }
                catch (SocketException e)
                {
                    if (e.ErrorCode == 10054)
                    {
                        // 远程主机强迫关闭了一个现有的连接。
                    }
                    else
                    {
                        PlatDependant.LogError(e);
                    }
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
            }
        }

        //https://stackoverflow.com/questions/5199026/c-sharp-async-udp-listener-socketexception
        public const int SIO_UDP_CONNRESET = -1744830452;
        protected static readonly byte[] SIO_UDP_CONNRESET_DATA = new byte[4];

        protected override IEnumerator ConnectWork()
        {
            try
            {
                KnownRemotes remotes = null;
                try
                {
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
                        try
                        {
                            _Socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, SIO_UDP_CONNRESET_DATA, null);
                        }
                        catch { }
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
                        try
                        {
                            _Socket6.IOControl((IOControlCode)SIO_UDP_CONNRESET, SIO_UDP_CONNRESET_DATA, null);
                        }
                        catch { }
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
                if (_ListenBroadcast)
                {
                    for (int i = 0; i < _SocketsBroadcast.Count; ++i)
                    {
                        var bsinfo = _SocketsBroadcast[i];
                        bsinfo.BeginReceive();
                    }
                    int knownRemotesVersion = 0;
                    while (!_ConnectWorkFinished)
                    {
                        int waitinterval;
                        try
                        {
                            bool knownRemotesChanged = false;
                            var curTick = Environment.TickCount;

                            for (int i = 0; i < _SocketsBroadcast.Count; ++i)
                            {
                                var bsinfo = _SocketsBroadcast[i];

                                RecvFromInfo recvmessages;
                                while (bsinfo.PendingRecvMessages.TryDequeue(out recvmessages))
                                {
                                    var messages = recvmessages.Buffers;
                                    var ep = recvmessages.Remote;
                                    for (int j = 0; j < messages.Count; ++j)
                                    {
                                        var message = messages[j];
                                        if (_OnReceive != null)
                                        {
                                            _OnReceive(message.Buffer, message.Length, ep);
                                        }
                                        message.Release();
                                    }
                                    remotes.Remotes[ep.Address] = new KnownRemote() { Address = ep.Address, LocalSocket = bsinfo.LocalSocket, LastTick = curTick };
                                    knownRemotesChanged = true;
                                    ReturnIPEndPointToPool(ep);
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

                            waitinterval = int.MinValue;
                            if (_OnUpdate != null)
                            {
                                waitinterval = _OnUpdate(this);
                            }

                            if (waitinterval == int.MinValue)
                            {
                                waitinterval = _UpdateInterval;
                                if (waitinterval < 0)
                                {
                                    waitinterval = CONST.MAX_WAIT_MILLISECONDS;
                                }
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
                }
                else
                {
                    _RemoteEP = new IPEndPoint(IPAddress.Any, _Port);
                    _RemoteEP6 = new IPEndPoint(IPAddress.IPv6Any, _Port);
                    BeginReceive4();
                    BeginReceive6();
                    while (!_ConnectWorkFinished)
                    {
                        int waitinterval;
                        try
                        {
                            RecvFromInfo recvmessages;
                            while (_PendingRecvMessages.TryDequeue(out recvmessages))
                            {
                                var messages = recvmessages.Buffers;
                                var ep = recvmessages.Remote ?? _Socket.RemoteEndPoint;
                                for (int i = 0; i < messages.Count; ++i)
                                {
                                    var message = messages[i];
                                    if (_OnReceive != null)
                                    {
                                        _OnReceive(message.Buffer, message.Length, ep);
                                    }
                                    message.Release();
                                }
                                ReturnIPEndPointToPool(recvmessages.Remote);
                            }

                            waitinterval = int.MinValue;
                            if (_OnUpdate != null)
                            {
                                waitinterval = _OnUpdate(this);
                            }

                            if (waitinterval == int.MinValue)
                            {
                                waitinterval = _UpdateInterval;
                                if (waitinterval < 0)
                                {
                                    waitinterval = CONST.MAX_WAIT_MILLISECONDS;
                                }
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
                }
            }
            finally
            {
                //_ConnectWorkStarted = false;
                //_ConnectWorkFinished = false;
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
                            bsinfo.LocalSocket = null;
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
        public override bool TrySend(MessageInfo minfo)
        {
            _HaveDataToSend.Set();
            Start();
            return false;
        }

        public void SendRaw(IPooledBuffer data, int cnt, IPEndPoint ep, Action<bool> onComplete)
        {
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("UDPServer Sending ");
                sb.Append(cnt);
                //for (int i = 0; i < cnt; ++i)
                //{
                //    if (i % 32 == 0)
                //    {
                //        sb.AppendLine();
                //    }
                //    sb.Append(data.Buffer[i].ToString("X2"));
                //    sb.Append(" ");
                //}
                PlatDependant.LogInfo(sb);
            }
#endif
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

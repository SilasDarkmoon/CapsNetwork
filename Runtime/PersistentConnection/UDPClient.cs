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
    public class UDPClient : ICustomSendConnection, IPositiveConnection, IDisposable
    {
        private string _Url;
        protected ReceiveHandler _OnReceive;
        protected int _UpdateInterval = -1;
        protected int _EaseUpdateRatio = 8;
        //protected SendCompleteHandler _OnSendComplete;
        protected CommonHandler _PreDispose;
        protected UpdateHandler _OnUpdate;
        protected SendHandler _OnSend;
        protected CommonHandler _PreStart;
        protected bool _WaitForBroadcastResp = false;
        protected bool _PositiveMode;

        protected UDPClient() { }
        public UDPClient(string url)
        {
            _Url = url;
        }

        public string Url
        {
            get { return _Url; }
            set
            {
                if (value != _Url)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change url when connection started");
                    }
                    else
                    {
                        _Url = value;
                    }
                }
            }
        }
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
        public int UpdateInterval
        {
            get { return _UpdateInterval; }
            set
            {
                if (value < 0)
                {
                    value = -1;
                }
                if (value != _UpdateInterval)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change UpdateInterval when connection started");
                    }
                    else
                    {
                        _UpdateInterval = value;
                    }
                }
            }
        }
        public int EaseUpdateRatio
        {
            get { return _EaseUpdateRatio; }
            set
            {
                if (value < 0)
                {
                    value = -1;
                }
                if (value != _EaseUpdateRatio)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change EaseUpdateRatio when connection started");
                    }
                    else
                    {
                        _EaseUpdateRatio = value;
                    }
                }
            }
        }
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
        /// <summary>
        /// This will be called in connection thread.
        /// </summary>
        public CommonHandler PreDispose
        {
            get { return _PreDispose; }
            set
            {
                if (value != _PreDispose)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change PreDispose when connection started");
                    }
                    else
                    {
                        _PreDispose = value;
                    }
                }
            }
        }
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
        /// <summary>
        /// This will be called in connection thread.
        /// </summary>
        public SendHandler OnSend
        {
            get { return _OnSend; }
            set
            {
                if (value != _OnSend)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change OnSend when connection started");
                    }
                    else
                    {
                        _OnSend = value;
                    }
                }
            }
        }
        /// <summary>
        /// This will be called in connection thread.
        /// </summary>
        public CommonHandler PreStart
        {
            get { return _PreStart; }
            set
            {
                if (value != _PreStart)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change PreStart when connection started");
                    }
                    else
                    {
                        _PreStart = value;
                    }
                }
            }
        }
        public bool WaitForBroadcastResp
        {
            get { return _WaitForBroadcastResp; }
            set
            {
                if (value != _WaitForBroadcastResp)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change WaitForBroadcastResp when connection started");
                    }
                    else
                    {
                        _WaitForBroadcastResp = value;
                    }
                }
            }
        }
        public bool PositiveMode
        {
            get { return _PositiveMode; }
            set
            {
                if (value != _PositiveMode)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change PositiveMode when connection started");
                    }
                    else
                    {
                        _PositiveMode = value;
                    }
                }
            }
        }

        protected volatile bool _ConnectWorkStarted;
        protected volatile bool _ConnectWorkFinished;
        protected Socket _Socket;
        public EndPoint RemoteEndPoint
        {
            get
            {
                if (_Socket != null)
                {
                    return _Socket.RemoteEndPoint;
                }
                return null;
            }
        }
        protected IPEndPoint _BroadcastEP;

        public bool IsAlive
        {
            get { return !_ConnectWorkFinished; }
        }
        public bool IsStarted
        {
            get { return _ConnectWorkStarted || _ConnectWorkFinished; }
        }
        protected IEnumerator _ConnectWork;
        public void Start()
        {
            if (!IsStarted)
            {
                _ConnectWorkStarted = true;
                if (_PositiveMode)
                {
                    _ConnectWork = ConnectWork();
                }
                else
                {
                    PlatDependant.RunBackground(prog =>
                    {
                        var work = ConnectWork();
                        while (work.MoveNext()) ;
                    });
                }
            }
        }
        public void Step()
        {
            if (_PositiveMode)
            {
                if (_ConnectWork != null)
                {
                    if (!_ConnectWork.MoveNext())
                    {
                        _ConnectWork = null;
                    }
                }
            }
        }

        public bool HoldSending = false;
        protected int _LastSendTick = int.MinValue;
        protected ConcurrentQueueGrowOnly<MessageInfo> _PendingSendMessages = new ConcurrentQueueGrowOnly<MessageInfo>();
        protected ConcurrentQueueGrowOnly<RecvFromInfo> _PendingRecvMessages = new ConcurrentQueueGrowOnly<RecvFromInfo>();
        //public static readonly byte[] EmptyBuffer = new byte[0];
        protected AutoResetEvent _HaveDataToSend = new AutoResetEvent(false);
        /// <summary>
        /// Schedule sending the data. Handle OnSendComplete to recyle the data buffer.
        /// </summary>
        /// <param name="data">data to be sent.</param>
        /// <returns>false means the data is dropped because to many messages is pending to be sent.</returns>
        public virtual bool TrySend(MessageInfo minfo)
        {
            if (!HoldSending && (Thread.CurrentThread.ManagedThreadId == _ConnectionThreadID || _PositiveMode))
            {
                DoSendWork(minfo);
            }
            else
            {
                _PendingSendMessages.Enqueue(minfo);
                _HaveDataToSend.Set();
                //StartConnect();
            }
            return true;
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

        public class SendAsyncInfo
        {
            public IPooledBuffer Data;
            public Socket Socket;
            public Action<bool> OnComplete;
            public AsyncCallback OnAsyncCallback;

            public void EndSend(IAsyncResult ar)
            {
                bool success = false;
                try
                {
                    Socket.EndSendTo(ar);
                    success = true;
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
                if (OnComplete != null)
                {
                    OnComplete(success);
                }
                Data.Release();
                ReturnSendAsyncInfoToPool(this);
            }

            public SendAsyncInfo()
            {
                OnAsyncCallback = EndSend;
            }
        }
        private static ConcurrentQueueFixedSize<SendAsyncInfo> _SendAsyncInfo = new ConcurrentQueueFixedSize<SendAsyncInfo>();
        public static SendAsyncInfo GetSendAsyncInfoFromPool()
        {
            SendAsyncInfo info;
            if (!_SendAsyncInfo.TryDequeue(out info))
            {
                info = new SendAsyncInfo();
            }
            return info;
        }
        public static void ReturnSendAsyncInfoToPool(SendAsyncInfo info)
        {
            if (info != null)
            {
                info.Data = null;
                info.Socket = null;
                info.OnComplete = null;
                _SendAsyncInfo.Enqueue(info);
            }
        }
        /// <summary>
        /// This should be called in connection thread. Real send data to server. The sending will NOT be done immediately, and we should NOT reuse data before onComplete.
        /// </summary>
        /// <param name="data">data to send.</param>
        /// <param name="cnt">data count in bytes.</param>
        /// <param name="onComplete">this will be called in some other thread.</param>
        public void SendRaw(IPooledBuffer data, int cnt, Action<bool> onComplete)
        {
            if (data != null)
            {
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(Environment.TickCount);
                    sb.Append(" UDPClient Sending ");
                    sb.Append(cnt);
                    for (int i = 0; i < cnt; ++i)
                    {
                        if (i % 32 == 0)
                        {
                            sb.AppendLine();
                        }
                        sb.Append(data.Buffer[i].ToString("X2"));
                        sb.Append(" ");
                    }
                    PlatDependant.LogInfo(sb);
                }
#endif
                data.AddRef();
                _LastSendTick = System.Environment.TickCount;
                if (_Socket != null)
                {
                    try
                    {
                        if (_BroadcastEP != null)
                        {
                            var info = GetSendAsyncInfoFromPool();
                            info.Data = data;
                            info.Socket = _Socket;
                            info.OnComplete = onComplete;
                            _Socket.BeginSendTo(data.Buffer, 0, cnt, SocketFlags.None, _BroadcastEP, info.OnAsyncCallback, null);
                            return;
                        }
                        else
                        {
                            var info = GetSendAsyncInfoFromPool();
                            info.Data = data;
                            info.Socket = _Socket;
                            info.OnComplete = onComplete;
                            _Socket.BeginSend(data.Buffer, 0, cnt, SocketFlags.None, info.OnAsyncCallback, null);
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                    }
                }
                if (onComplete != null)
                {
                    onComplete(false);
                }
                data.Release();
            }
        }
        public void SendRaw(IPooledBuffer data, int cnt)
        {
            SendRaw(data, cnt, null);
        }
        public void SendRaw(IPooledBuffer data)
        {
            SendRaw(data, data.Buffer.Length);
        }
        public void SendRaw(byte[] data, int cnt, Action<bool> onComplete)
        {
            SendRaw(new UnpooledBuffer(data), cnt, onComplete);
        }
        //public void SendRaw(byte[] data, int cnt, Action onComplete)
        //{
        //    SendRaw(data, cnt, onComplete == null ? null : (Action<bool>)(success => onComplete()));
        //}
        public void SendRaw(byte[] data, int cnt)
        {
            SendRaw(data, cnt, null);
        }
        public void SendRaw(byte[] data)
        {
            SendRaw(data, data.Length);
        }

        private static ConcurrentQueueFixedSize<IPEndPoint> _IPEndPointPool = new ConcurrentQueueFixedSize<IPEndPoint>();
        public static IPEndPoint GetIPEndPointFromPool()
        {
            IPEndPoint ep;
            if (!_IPEndPointPool.TryDequeue(out ep))
            {
                ep = new IPEndPoint(IPAddress.Any, 0);
            }
            return ep;
        }
        public static void ReturnIPEndPointToPool(IPEndPoint ep)
        {
            if (ep != null)
            {
                _IPEndPointPool.Enqueue(ep);
            }
        }
        protected struct RecvFromInfo
        {
            public IPEndPoint Remote;
            public ValueList<PooledBufferSpan> Buffers;
        }

        protected byte[] _ReceiveBuffer = new byte[CONST.MTU];
        protected EndPoint _RemoteEP;
        protected void EndReceiveFrom(IAsyncResult ar)
        {
            try
            {
                var receivecnt = _Socket.EndReceiveFrom(ar, ref _RemoteEP);
                if (receivecnt > 0)
                {
                    if (_WaitForBroadcastResp)
                    {
                        var ep = GetIPEndPointFromPool();
                        ep.Address = ((IPEndPoint)_RemoteEP).Address;
                        ep.Port = ((IPEndPoint)_RemoteEP).Port;

                        _PendingRecvMessages.Enqueue(new RecvFromInfo() { Buffers = BufferPool.GetPooledBufferList(_ReceiveBuffer, 0, receivecnt), Remote = ep });
                    }
                    else
                    {
                        _Socket.Connect(_RemoteEP);
                        _BroadcastEP = null;
                        _PendingRecvMessages.Enqueue(new RecvFromInfo() { Buffers = BufferPool.GetPooledBufferList(_ReceiveBuffer, 0, receivecnt) });
                    }
                }
                if (!_ConnectWorkFinished)
                {
                    BeginReceive();
                }
            }
            catch (Exception e)
            {
                if (IsAlive)
                {
                    _ConnectWorkFinished = true;
                    PlatDependant.LogError(e);
                }
            }
            _HaveDataToSend.Set();
        }
        protected AsyncCallback EndReceiveFromFunc;
        protected void EndReceive(IAsyncResult ar)
        {
            try
            {
                var receivecnt = _Socket.EndReceive(ar);
#if DEBUG_PERSIST_CONNECT_LOW_LEVEL
                if (receivecnt > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append("UDPClient Receiving ");
                    sb.Append(receivecnt);
                    for (int i = 0; i < receivecnt; ++i)
                    {
                        if (i % 32 == 0)
                        {
                            sb.AppendLine();
                        }
                        sb.Append(_ReceiveBuffer[i].ToString("X2"));
                        sb.Append(" ");
                    }
                    PlatDependant.LogInfo(sb);
                }
#endif
                if (receivecnt > 0)
                {
                    _PendingRecvMessages.Enqueue(new RecvFromInfo() { Buffers = BufferPool.GetPooledBufferList(_ReceiveBuffer, 0, receivecnt) });
                }
                if (!_ConnectWorkFinished)
                {
                    BeginReceive();
                }
            }
            catch (Exception e)
            {
                if (IsAlive)
                {
                    _ConnectWorkFinished = true;
                    PlatDependant.LogError(e);
                }
            }
            _HaveDataToSend.Set();
        }
        protected AsyncCallback EndReceiveFunc;
        protected void BeginReceive()
        {
            try
            {
                if (_BroadcastEP != null)
                {
                    var cb = EndReceiveFromFunc = EndReceiveFromFunc ?? EndReceiveFrom;
                    _Socket.BeginReceiveFrom(_ReceiveBuffer, 0, CONST.MTU, SocketFlags.None, ref _RemoteEP, cb, null);
                }
                else
                {
                    var cb = EndReceiveFunc = EndReceiveFunc ?? EndReceive;
                    _Socket.BeginReceive(_ReceiveBuffer, 0, CONST.MTU, SocketFlags.None, cb, null);
                }
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
            }
        }

        protected int _ConnectionThreadID;
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
                DoSendWork(message);
            }
        }
        protected void DoSendWork(PooledBufferSpan message)
        {
            var cnt = message.Length;
            if (_OnSend != null && _OnSend(message, cnt))
            {
                //if (_OnSendComplete != null)
                //{
                //    _OnSendComplete(true);
                //}
            }
            else
            {
                SendRaw(message, cnt
                    //, success =>
                    //{
                    //    if (_OnSendComplete != null)
                    //    {
                    //        _OnSendComplete(message, success);
                    //    }
                    //}
                    );
            }
            message.Release();
        }
        protected virtual IEnumerator ConnectWork()
        {
            try
            {
                try
                {
                    _ConnectionThreadID = Thread.CurrentThread.ManagedThreadId;
                    if (_Url != null)
                    {
                        bool isMulticastOrBroadcast = false;
                        int port = 0;

                        Uri uri = new Uri(_Url);
                        port = uri.Port;
                        var addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
                        if (addresses != null && addresses.Length > 0)
                        {
                            var address = addresses[0];
                            if (address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                if (address.Equals(IPAddress.Broadcast))
                                {
                                    isMulticastOrBroadcast = true;
                                }
                                else
                                {
                                    var firstb = address.GetAddressBytes()[0];
                                    if (firstb >= 224 && firstb < 240)
                                    {
                                        isMulticastOrBroadcast = true;
                                    }
                                }
                            }
                            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                            {
                                if (address.IsIPv6Multicast)
                                {
                                    isMulticastOrBroadcast = true;
                                }
                            }
                            _Socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                            if (isMulticastOrBroadcast)
                            {
                                _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                                if (address.AddressFamily == AddressFamily.InterNetworkV6)
                                {
#if NET_STANDARD_2_0 || NET_4_6 || !UNITY_ENGINE && !UNITY_5_3_OR_NEWER
                                    // Notice: it is a pitty that unity does not support ipv6 multicast. (Unity 5.6)
                                    _Socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(address));
                                    _Socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastTimeToLive, 5);
#endif
                                    _Socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
                                }
                                else
                                {
                                    if (!address.Equals(IPAddress.Broadcast))
                                    {
                                        _Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(address, IPAddress.Any));
                                        _Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 5);
                                    }
                                    _Socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                                }
                                _BroadcastEP = new IPEndPoint(address, port);
                            }
                            else
                            {
                                _Socket.Connect(address, port);
                            }
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
                if (_Socket != null)
                {
                    if (_PreStart != null)
                    {
                        _PreStart(this);
                    }
                    byte[] receivebuffer = new byte[CONST.MTU];
                    if (_BroadcastEP != null && _BroadcastEP.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        _RemoteEP = new IPEndPoint(IPAddress.IPv6Any, 0);
                    }
                    else
                    {
                        _RemoteEP = new IPEndPoint(IPAddress.Any, 0);
                    }
                    if (_OnReceive != null)
                    {
                        BeginReceive();
                    }
                    while (!_ConnectWorkFinished)
                    {
                        int waitinterval;
                        try
                        {
                            if (_OnReceive != null)
                            {
                                RecvFromInfo recvmessages;
                                while (_PendingRecvMessages.TryDequeue(out recvmessages))
                                {
                                    var messages = recvmessages.Buffers;
                                    var ep = recvmessages.Remote ?? _Socket.RemoteEndPoint;
                                    for (int i = 0; i < messages.Count; ++i)
                                    {
                                        var message = messages[i];
                                        _OnReceive(message.Buffer, message.Length, ep);
                                        message.Release();
                                    }
                                    ReturnIPEndPointToPool(recvmessages.Remote);
                                }
                            }

                            if (!HoldSending)
                            {
                                MessageInfo minfo;
                                while (_PendingSendMessages.TryDequeue(out minfo))
                                {
                                    DoSendWork(minfo);
                                }
                            }

                            waitinterval = int.MinValue;
                            if (_OnUpdate != null)
                            {
                                waitinterval = _OnUpdate(this);
                            }

                            if (waitinterval == int.MinValue)
                            {
                                waitinterval = _UpdateInterval;
                                var easeratio = _EaseUpdateRatio;
                                if (waitinterval > 0 && easeratio > 0)
                                {
                                    var easeinterval = waitinterval * easeratio;
                                    if (_LastSendTick + easeinterval <= System.Environment.TickCount)
                                    {
                                        waitinterval = easeinterval;
                                    }
                                }
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
                // set handlers to null.
                _OnReceive = null;
                _OnSend = null;
                //_OnSendComplete = null;
                _OnUpdate = null;
                _PreDispose = null;
            }
        }

        public void Dispose()
        {
            Dispose(false);
        }
        public void Dispose(bool inFinalizer)
        {
            if (_ConnectWorkStarted)
            {
                _ConnectWorkFinished = true;
                if (_PositiveMode)
                {
                    var disposable = _ConnectWork as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                    _ConnectWork = null;
                }
                else
                {
                    _HaveDataToSend.Set();
                }
            }
            if (!inFinalizer)
            {
                GC.SuppressFinalize(this);
            }
        }
        ~UDPClient()
        {
            Dispose(true);
        }
    }

    public static partial class ConnectionFactory
    {
        private static RegisteredCreator _Reg_UDP = new RegisteredCreator("udp"
            , uri => new UDPClient(uri.ToString())
            , null);
    }
}
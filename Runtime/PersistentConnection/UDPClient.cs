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
    public class UDPClient : ICustomSendConnection, IDisposable
    {
        private string _Url;
        protected ReceiveHandler _OnReceive;
        protected int _UpdateInterval = -1;
        protected int _EaseUpdateRatio = 8;
        protected SendCompleteHandler _OnSendComplete;
        protected CommonHandler _PreDispose;
        protected CommonHandler _OnUpdate;
        protected SendHandler _OnSend;
        protected CommonHandler _PreStart;
        protected bool _WaitForBroadcastResp = false;

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
                    if (IsConnectionAlive)
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
                    if (IsConnectionAlive)
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
                    if (IsConnectionAlive)
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
        /// <summary>
        /// This will be called in undetermined thread.
        /// </summary>
        public SendCompleteHandler OnSendComplete
        {
            get { return _OnSendComplete; }
            set
            {
                if (value != _OnSendComplete)
                {
                    if (IsConnectionAlive)
                    {
                        PlatDependant.LogError("Cannot change OnSendComplete when connection started");
                    }
                    else
                    {
                        _OnSendComplete = value;
                    }
                }
            }
        }
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
                    if (IsConnectionAlive)
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
        public CommonHandler OnUpdate
        {
            get { return _OnUpdate; }
            set
            {
                if (value != _OnUpdate)
                {
                    if (IsConnectionAlive)
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
                    if (IsConnectionAlive)
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
                    if (IsConnectionAlive)
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
                    if (IsConnectionAlive)
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

        protected bool _ConnectWorkRunning;
        protected bool _ConnectWorkCanceled;
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

        public bool IsConnectionAlive
        {
            get { return _ConnectWorkRunning && !_ConnectWorkCanceled; }
        }
        public void StartConnect()
        {
            if (!IsConnectionAlive)
            {
                _ConnectWorkRunning = true;
                PlatDependant.RunBackground(prog => ConnectWork());
            }
        }

        public bool HoldSending = false;
        protected int _LastSendTick = int.MinValue;
        protected ConcurrentQueue<BufferInfo> _PendingSendMessages = new ConcurrentQueue<BufferInfo>();
        protected AutoResetEvent _HaveDataToSend = new AutoResetEvent(false);
        /// <summary>
        /// Schedule sending the data. Handle OnSendComplete to recyle the data buffer.
        /// </summary>
        /// <param name="data">data to be sent.</param>
        /// <returns>false means the data is dropped because to many messages is pending to be sent.</returns>
        public bool TrySend(byte[] data, int cnt)
        {
            _PendingSendMessages.Enqueue(new BufferInfo(data, cnt));
            _HaveDataToSend.Set();
            //StartConnect();
            return true;
        }
        public virtual void Send(byte[] data, int cnt)
        {
            TrySend(data, cnt);
        }
        public void Send(byte[] data)
        {
            Send(data, data.Length);
        }
        /// <summary>
        /// This should be called in connection thread. Real send data to server. The sending will NOT be done immediately, and we should NOT reuse data before onComplete.
        /// </summary>
        /// <param name="data">data to send.</param>
        /// <param name="cnt">data count in bytes.</param>
        /// <param name="onComplete">this will be called in some other thread.</param>
        public void SendRaw(byte[] data, int cnt, Action<bool> onComplete)
        {
            _LastSendTick = System.Environment.TickCount;
            if (_Socket != null)
            {
                try
                {
                    if (_BroadcastEP != null)
                    {
                        _Socket.BeginSendTo(data, 0, cnt, SocketFlags.None, _BroadcastEP, ar =>
                        {
                            bool success = false;
                            try
                            {
                                _Socket.EndSendTo(ar);
                                success = true;
                            }
                            catch (Exception e)
                            {
                                PlatDependant.LogError(e);
                            }
                            if (onComplete != null)
                            {
                                onComplete(success);
                            }
                        }, null);
                        return;
                    }
                    else
                    {
                        _Socket.BeginSend(data, 0, cnt, SocketFlags.None, ar =>
                        {
                            bool success = false;
                            try
                            {
                                _Socket.EndSend(ar);
                                success = true;
                            }
                            catch (Exception e)
                            {
                                PlatDependant.LogError(e);
                            }
                            if (onComplete != null)
                            {
                                onComplete(success);
                            }
                        }, null);
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
        }
        public void SendRaw(byte[] data, int cnt, Action onComplete)
        {
            SendRaw(data, cnt, onComplete == null ? null : (Action<bool>)(success => onComplete()));
        }
        public void SendRaw(byte[] data, int cnt)
        {
            SendRaw(data, cnt, (Action<bool>)null);
        }
        public void SendRaw(byte[] data)
        {
            SendRaw(data, data.Length);
        }
        protected virtual void ConnectWork()
        {
            try
            {
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
#if NET_STANDARD_2_0 || NET_4_6
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
                if (_Socket != null)
                {
                    if (_PreStart != null)
                    {
                        _PreStart(this);
                    }
                    byte[] receivebuffer = new byte[CONST.MTU];
                    int receivecnt = 0;
                    EndPoint broadcastRespEP;
                    if (_BroadcastEP != null && _BroadcastEP.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        broadcastRespEP = new IPEndPoint(IPAddress.IPv6Any, 0);
                    }
                    else
                    {
                        broadcastRespEP = new IPEndPoint(IPAddress.Any, 0);
                    }
                    Action BeginReceive = () =>
                    {
                        if (_BroadcastEP != null)
                        {
                            _Socket.BeginReceiveFrom(receivebuffer, 0, CONST.MTU, SocketFlags.None, ref broadcastRespEP, ar =>
                            {
                                try
                                {
                                    receivecnt = _Socket.EndReceiveFrom(ar, ref broadcastRespEP);
                                }
                                catch (Exception e)
                                {
                                    if (IsConnectionAlive)
                                    {
                                        _ConnectWorkCanceled = true;
                                        PlatDependant.LogError(e);
                                    }
                                }
                                _HaveDataToSend.Set();
                            }, null);
                        }
                        else
                        {
                            _Socket.BeginReceive(receivebuffer, 0, CONST.MTU, SocketFlags.None, ar =>
                            {
                                try
                                {
                                    receivecnt = _Socket.EndReceive(ar);
                                }
                                catch (Exception e)
                                {
                                    if (IsConnectionAlive)
                                    {
                                        _ConnectWorkCanceled = true;
                                        PlatDependant.LogError(e);
                                    }
                                }
                                _HaveDataToSend.Set();
                            }, null);
                        }
                    };
                    if (_OnReceive != null)
                    {
                        BeginReceive();
                    }
                    while (!_ConnectWorkCanceled)
                    {
                        if (_OnReceive != null)
                        {
                            if (receivecnt > 0)
                            {
                                if (_BroadcastEP != null && _WaitForBroadcastResp)
                                {
                                    _OnReceive(receivebuffer, receivecnt, broadcastRespEP);
                                    receivecnt = 0;
                                    BeginReceive();
                                }
                                else
                                {
                                    if (_BroadcastEP != null)
                                    {
                                        _Socket.Connect(broadcastRespEP);
                                        _BroadcastEP = null;
                                    }
                                    _OnReceive(receivebuffer, receivecnt, _Socket.RemoteEndPoint);
                                    receivecnt = 0;
                                    BeginReceive();
                                }
                            }
                        }

                        if (!HoldSending)
                        {
                            BufferInfo binfo;
                            while (_PendingSendMessages.TryDequeue(out binfo))
                            {
                                var message = binfo.Buffer;
                                int cnt = binfo.Count;
                                if (_OnSend != null && _OnSend(message, cnt))
                                {
                                    if (_OnSendComplete != null)
                                    {
                                        _OnSendComplete(message, true);
                                    }
                                }
                                else
                                {
                                    SendRaw(message, cnt, success =>
                                    {
                                        if (_OnSendComplete != null)
                                        {
                                            _OnSendComplete(message, success);
                                        }
                                    });
                                }
                            }
                        }

                        if (_OnUpdate != null)
                        {
                            _OnUpdate(this);
                        }

                        var waitinterval = _UpdateInterval;
                        var easeratio = _EaseUpdateRatio;
                        if (waitinterval > 0 && easeratio > 0)
                        {
                            var easeinterval = waitinterval * easeratio;
                            if (_LastSendTick + easeinterval <= System.Environment.TickCount)
                            {
                                waitinterval = easeinterval;
                            }
                        }
                        _HaveDataToSend.WaitOne(waitinterval);
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
                // set handlers to null.
                _OnReceive = null;
                _OnSend = null;
                _OnSendComplete = null;
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
            if (_ConnectWorkRunning)
            {
                _ConnectWorkCanceled = true;
                _HaveDataToSend.Set();
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

    public static partial class PersistentConnectionFactory
    {
        private static RegisteredCreator _Reg_UDP = new RegisteredCreator("udp"
            , url => new UDPClient(url)
            , null);
    }
}
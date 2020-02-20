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
    public class TCPClient : ICustomSendConnection, IPositiveConnection, IDisposable
    {
        private string _Url;
        protected ReceiveHandler _OnReceive;
        //protected SendCompleteHandler _OnSendComplete;
        protected CommonHandler _PreDispose;
        protected SendHandler _OnSend;
        protected UpdateHandler _OnUpdate;
        protected bool _PositiveMode;

        protected TCPClient() { }
        public TCPClient(string url)
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
        public UpdateHandler OnUpdate
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
        public bool PositiveMode
        {
            get { return _PositiveMode; }
            set
            {
                if (value != _PositiveMode)
                {
                    if (IsConnectionAlive)
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

        protected volatile bool _ConnectWorkRunning;
        protected volatile bool _ConnectWorkCanceled;
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

        public bool IsConnectionAlive
        {
            get { return _ConnectWorkRunning && !_ConnectWorkCanceled; }
        }
        protected IEnumerator _ConnectWork;
        public void StartConnect()
        {
            if (!IsConnectionAlive)
            {
                _ConnectWorkRunning = true;
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

        protected ConcurrentQueueGrowOnly<MessageInfo> _PendingSendMessages = new ConcurrentQueueGrowOnly<MessageInfo>();
        protected AutoResetEvent _HaveDataToSend = new AutoResetEvent(false);
        /// <summary>
        /// Schedule sending the data. Handle OnSendComplete to recyle the data buffer.
        /// </summary>
        /// <param name="data">data to be sent.</param>
        /// <returns>false means the data is dropped because to many messages is pending to be sent.</returns>
        public virtual bool TrySend(MessageInfo minfo)
        {
            if (Thread.CurrentThread.ManagedThreadId == _ConnectionThreadID || _PositiveMode)
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

        /// <summary>
        /// This should be called in connection thread. Real send data to server. The sending will NOT be done immediately, and we should NOT reuse data before onComplete.
        /// </summary>
        /// <param name="data">data to send.</param>
        /// <param name="cnt">data count in bytes.</param>
        /// <param name="onComplete">this will be called in some other thread.</param>
        public void SendRaw(IPooledBuffer data, int cnt, Action<bool> onComplete)
        {
            data.AddRef();
            if (_Socket != null)
            {
                try
                {
                    var info = UDPClient.GetSendAsyncInfoFromPool();
                    info.Data = data;
                    info.Socket = _Socket;
                    info.OnComplete = onComplete;
                    _Socket.BeginSend(data.Buffer, 0, cnt, SocketFlags.None, info.OnAsyncCallback, null);
                    return;
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
        public void SendRaw(IPooledBuffer data, int cnt)
        {
            SendRaw(data, cnt, null);
        }
        public void SendRaw(IPooledBuffer data)
        {
            SendRaw(data, data.Buffer.Length);
        }
        //public void SendRaw(byte[] data, int cnt, Action onComplete)
        //{
        //    SendRaw(data, cnt, onComplete == null ? null : (Action<bool>)(success => onComplete()));
        //}
        public void SendRaw(byte[] data, int cnt, Action<bool> onComplete)
        {
            SendRaw(new UnpooledBuffer(data), cnt, onComplete);
        }
        public void SendRaw(byte[] data, int cnt)
        {
            SendRaw(data, cnt, null);
        }
        public void SendRaw(byte[] data)
        {
            SendRaw(data, data.Length);
        }
        protected virtual void PrepareSocket()
        {
            if (_Url != null)
            {
                Uri uri = new Uri(_Url);
                var addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
                if (addresses != null && addresses.Length > 0)
                {
                    var address = addresses[0];
                    _Socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _Socket.Connect(address, uri.Port);
                }
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
                _ConnectionThreadID = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    PrepareSocket();
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
                    byte[] receivebuffer = new byte[CONST.MTU];
                    int receivecnt = 0;
                    Action BeginReceive = () =>
                    {
                        try
                        {
                            _Socket.BeginReceive(receivebuffer, 0, 1, SocketFlags.None, ar =>
                            {
                                try
                                {
                                    receivecnt = _Socket.EndReceive(ar);
                                    if (receivecnt > 0)
                                    {
                                        var bytesRemaining = _Socket.Available;
                                        if (bytesRemaining > 0)
                                        {
                                            if (bytesRemaining > CONST.MTU - 1)
                                            {
                                                bytesRemaining = CONST.MTU - 1;
                                            }
                                            receivecnt += _Socket.Receive(receivebuffer, 1, bytesRemaining, SocketFlags.None);
                                        }
                                    }
                                    else
                                    {
                                        if (_ConnectWorkRunning)
                                        {
                                            _ConnectWorkCanceled = true;
                                        }
                                    }
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
                        catch (Exception e)
                        {
                            PlatDependant.LogError(e);
                        }
                    };
                    BeginReceive();
                    while (!_ConnectWorkCanceled)
                    {
                        int waitinterval;
                        try
                        {
                            if (receivecnt > 0)
                            {
                                if (_OnReceive != null)
                                {
                                    _OnReceive(receivebuffer, receivecnt, _Socket.RemoteEndPoint);
                                }
                                receivecnt = 0;
                                BeginReceive();
                            }

                            MessageInfo minfo;
                            while (_PendingSendMessages.TryDequeue(out minfo))
                            {
                                DoSendWork(minfo);
                            }

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
                        if (_PositiveMode)
                        {
                            yield return null;
                        }
                        else
                        {
                            _HaveDataToSend.WaitOne(waitinterval);
                        }
                    }
                    _Socket.Shutdown(SocketShutdown.Both);
                }
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
                //_OnSendComplete = null;
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
        ~TCPClient()
        {
            Dispose(true);
        }
    }
}

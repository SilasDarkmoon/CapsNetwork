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
using Unity.Collections.Concurrent;
#else
using System.Collections.Concurrent;
#endif

using PlatDependant = Capstones.UnityEngineEx.PlatDependant;
using TaskProgress = Capstones.UnityEngineEx.TaskProgress;

namespace Capstones.Net
{
    public class TCPClient : ICustomSendConnection, IDisposable
    {
        private string _Url;
        protected ReceiveHandler _OnReceive;
        protected SendCompleteHandler _OnSendComplete;
        protected CommonHandler _PreDispose;
        protected SendHandler _OnSend;

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
            if (_Socket != null)
            {
                try
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
        protected virtual void ConnectWork()
        {
            try
            {
                PrepareSocket();
                if (_Socket != null)
                {
                    byte[] receivebuffer = new byte[CONST.MTU];
                    int receivecnt = 0;
                    Action BeginReceive = () =>
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
                    };
                    BeginReceive();
                    while (!_ConnectWorkCanceled)
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

                        _HaveDataToSend.WaitOne();
                    }
                    _Socket.Shutdown(SocketShutdown.Both);
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
        ~TCPClient()
        {
            Dispose(true);
        }
    }
}

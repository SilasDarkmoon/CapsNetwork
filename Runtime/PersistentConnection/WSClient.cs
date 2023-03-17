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
    public class WSClient : ICustomSendConnection, IPositiveConnection, IDisposable, IAutoPackedConnection
    {
        private string _Url;
        protected ReceiveHandler _OnReceive;
        //protected SendCompleteHandler _OnSendComplete;
        protected CommonHandler _OnClose;
        protected SendHandler _OnSend;
        protected UpdateHandler _OnUpdate;
        protected bool _PositiveMode;

        protected WSClient() { }
        public WSClient(string url)
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
        public CommonHandler OnClose
        {
            get { return _OnClose; }
            set
            {
                if (value != _OnClose)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change PreDispose when connection started");
                    }
                    else
                    {
                        _OnClose = value;
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
        protected System.Net.WebSockets.ClientWebSocket _WebSocket;
        public EndPoint RemoteEndPoint
        {
            get
            {
                return null;
            }
        }

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
                    PlatDependant.RunBackgroundLongTime(prog =>
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

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        protected ConcurrentQueueGrowOnly<MessageInfo> _PendingSendMessages = new ConcurrentQueueGrowOnly<MessageInfo>();
        protected ConcurrentQueueGrowOnly<ValueList<PooledBufferSpan>> _PendingRecvMessages = new ConcurrentQueueGrowOnly<ValueList<PooledBufferSpan>>();
#else
        protected ConcurrentQueue<MessageInfo> _PendingSendMessages = new ConcurrentQueue<MessageInfo>();
        protected ConcurrentQueue<ValueList<PooledBufferSpan>> _PendingRecvMessages = new ConcurrentQueue<ValueList<PooledBufferSpan>>();
#endif
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

#if LOG_TCPCLIENT_SEND_DATA
        private string _LogFilePath;
        public string LogFilePath
        {
            get
            {
                if (_LogFilePath == null)
                {
                    _LogFilePath = System.IO.Path.Combine(ThreadSafeValues.LogPath, $"rec/tcpdata{DateTime.Now:ddHHmmss}.bin");
                }
                return _LogFilePath;
            }
        }
#endif

        protected AutoResetEvent _AsyncSendWaitHandle = new AutoResetEvent(true);
        protected struct PendingSendAsyncMessageInfo
        {
            public IPooledBuffer Buffer;
            public int Length;
            public Action<bool> OnComplete;
        }
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        protected ConcurrentQueueGrowOnly<PendingSendAsyncMessageInfo> _SendingAsyncMessages = new ConcurrentQueueGrowOnly<PendingSendAsyncMessageInfo>();
#else
        protected ConcurrentQueue<PendingSendAsyncMessageInfo> _SendingAsyncMessages = new ConcurrentQueue<PendingSendAsyncMessageInfo>();
#endif
        public void SendRawWork()
        {
            try
            {
                while (true)
                {
                    _AsyncSendWaitHandle.WaitOne(500);
                    if (_ConnectWorkFinished)
                    {
                        break;
                    }
                    PendingSendAsyncMessageInfo minfo;
                    while (_SendingAsyncMessages.TryDequeue(out minfo))
                    {
                        var wsocket = _WebSocket;
                        if (wsocket == null)
                        {
                            minfo.Buffer.Release();
                            if (minfo.OnComplete != null)
                            {
                                try
                                {
                                    minfo.OnComplete(false);
                                }
                                catch (Exception e)
                                {
                                    PlatDependant.LogError(e);
                                }
                            }
                            return;
                        }
                        var task = wsocket.SendAsync(new ArraySegment<byte>(minfo.Buffer.Buffer, 0, minfo.Length), System.Net.WebSockets.WebSocketMessageType.Binary, true, CancellationToken.None);
                        while (!task.Wait(500))
                        {
                            if (_ConnectWorkFinished)
                            {
                                minfo.Buffer.Release();
                                if (minfo.OnComplete != null)
                                {
                                    try
                                    {
                                        minfo.OnComplete(false);
                                    }
                                    catch (Exception e)
                                    {
                                        PlatDependant.LogError(e);
                                    }
                                }
                                return;
                            }
                        }
                        minfo.Buffer.Release();
                        if (minfo.OnComplete != null)
                        {
                            try
                            {
                                minfo.OnComplete(true);
                            }
                            catch (Exception e)
                            {
                                PlatDependant.LogError(e);
                            }
                        }
                    }
                }
            }
            finally
            {
                PendingSendAsyncMessageInfo minfo;
                while (_SendingAsyncMessages.TryDequeue(out minfo))
                {
                    minfo.Buffer.Release();
                    if (minfo.OnComplete != null)
                    {
                        try
                        {
                            minfo.OnComplete(false);
                        }
                        catch (Exception e)
                        {
                            PlatDependant.LogError(e);
                        }
                    }
                }
            }
        }
        public void BeginSendRawWork()
        {
            PlatDependant.RunBackgroundLongTime(prog =>
            {
                SendRawWork();
            });
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
            _SendingAsyncMessages.Enqueue(new PendingSendAsyncMessageInfo()
            {
                Buffer = data,
                Length = cnt,
                OnComplete = onComplete,
            });
            _AsyncSendWaitHandle.Set();
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

        protected byte[] _ReceiveBuffer = new byte[CONST.MTU];
        protected byte[] _ReceiveMessageBuffer;
        protected int _ReceivedMessageBufferCount = 0;
        protected void EndReceive(System.Net.WebSockets.WebSocketReceiveResult result)
        {
            try
            {
                var oldcnt = _ReceivedMessageBufferCount;
                var cnt = result.Count;
                if (_ReceiveMessageBuffer == null)
                {
                    _ReceiveMessageBuffer = new byte[cnt];
                }
                else if (_ReceiveMessageBuffer.Length < oldcnt + cnt)
                {
                    var oldbuffer = _ReceiveMessageBuffer;
                    _ReceiveMessageBuffer = new byte[oldcnt + cnt];
                    Buffer.BlockCopy(oldbuffer, 0, _ReceiveMessageBuffer, 0, oldcnt);
                }
                Buffer.BlockCopy(_ReceiveBuffer, 0, _ReceiveMessageBuffer, oldcnt, cnt);
                _ReceivedMessageBufferCount += cnt;

                if (result.EndOfMessage)
                {
                    _PendingRecvMessages.Enqueue(new ValueList<PooledBufferSpan>() { new PooledBufferSpan()
                    {
                        WholeBuffer = new UnpooledBuffer(_ReceiveMessageBuffer),
                        Length = _ReceivedMessageBufferCount,
                    } });
                    _ReceiveMessageBuffer = null;
                    _ReceivedMessageBufferCount = 0;
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
        protected async void BeginReceive()
        {
            try
            {
                var result = await _WebSocket.ReceiveAsync(new ArraySegment<byte>(_ReceiveBuffer), CancellationToken.None);
                EndReceive(result);
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                Dispose();
            }
        }

        protected IDictionary<string, string> _Headers;
        public IDictionary<string, string> Headers
        {
            get { return _Headers; }
            set
            {
                if (value != _Headers)
                {
                    if (IsStarted)
                    {
                        PlatDependant.LogError("Cannot change Headers when connection started");
                    }
                    else
                    {
                        _Headers = value;
                    }
                }
            }
        }
        protected virtual void PrepareSocket()
        {
            if (_Url != null)
            {
                Uri uri = new Uri(_Url);
                _WebSocket = new System.Net.WebSockets.ClientWebSocket();
                var headers = _Headers;
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        _WebSocket.Options.SetRequestHeader(kvp.Key, kvp.Value);
                    }
                }
                var task = _WebSocket.ConnectAsync(uri, CancellationToken.None);
                while (!task.Wait(500))
                {
                    if (_ConnectWorkFinished)
                    {
                        throw new ObjectDisposedException("WebSocket");
                    }
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
            int total = 0;
            for (int i = 0; i < messages.Count; ++i)
            {
                var message = messages[i];
                total += message.Length;
            }
            byte[] sendBuffer = new byte[total];
            total = 0;
            for (int i = 0; i < messages.Count; ++i)
            {
                var message = messages[i];
                var sbuffer = message.Buffer;
                var length = message.Length;
                Buffer.BlockCopy(sbuffer, 0, sendBuffer, total, length);
                total += length;
            }
            DoSendWork(new PooledBufferSpan()
            {
                WholeBuffer = new UnpooledBuffer(sendBuffer),
                Length = total,
            });
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
                if (_WebSocket != null)
                {
                    BeginReceive();
                    BeginSendRawWork();
                    while (!_ConnectWorkFinished)
                    {
                        int waitinterval;
                        try
                        {
                            ValueList<PooledBufferSpan> recvmessages;
                            while (_PendingRecvMessages.TryDequeue(out recvmessages))
                            {
                                for (int i = 0; i < recvmessages.Count; ++i)
                                {
                                    var message = recvmessages[i];
                                    if (_OnReceive != null)
                                    {
                                        _OnReceive(message.Buffer, message.Length, null);
                                    }
                                    message.Release();
                                }
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
                    try
                    {
                        var closetask = _WebSocket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Timeout or client-positive", CancellationToken.None);
                        if (!closetask.Wait(1000))
                        {
                            throw new TimeoutException("WebSocket close timedout.");
                        }
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                        try
                        {
                            _WebSocket.Abort();
                        }
                        catch (Exception ex)
                        {
                            PlatDependant.LogError(ex);
                        }
                    }
                }
            }
            finally
            {
                _ConnectWorkFinished = true;
                //_ConnectWorkRunning = false;
                //_ConnectWorkCanceled = false;
                if (_OnClose != null)
                {
                    _OnClose(this);
                }
                if (_WebSocket != null)
                {
                    _WebSocket.Dispose();
                    _WebSocket = null;
                }
                // set handlers to null.
                _OnReceive = null;
                _OnSend = null;
                _OnUpdate = null;
                //_OnSendComplete = null;
                _OnClose = null;
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
        ~WSClient()
        {
            Dispose(true);
        }
    }

    public static partial class ConnectionFactory
    {
        private static RegisteredCreator _Reg_WebSocket = new RegisteredCreator("ws"
            , (uri, exconfig) => new WSClient(uri.ToString()) { Headers = exconfig.Get<IDictionary<string, string>>("headers") }
            , null);
    }
}

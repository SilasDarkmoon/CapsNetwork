using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;
using System.IO;
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
    public abstract class Request : IDisposable
    {
        protected object _RequestObj;
        public object RequestObj { get { return _RequestObj; } }

        private object _ResponseObj;
        public object ResponseObj
        {
            get { return _ResponseObj; }
            protected set
            {
                _ResponseObj = value;
                _FinishTick = Environment.TickCount;
                _RTT = _FinishTick - _StartTick;
                Done = true;
            }
        }
        public T GetResponse<T>()
        {
            return _ResponseObj is T ? (T)_ResponseObj : default(T);
        }

        public event Action OnDone = () => { };
        private bool _Done;
        public bool Done
        {
            get { return _Done; }
            protected set
            {
                var old = _Done;
                _Done = value;
                if (!old && value)
                {
                    OnDone();
                }
            }
        }
        private object _Error;
        public object Error
        {
            get { return _Error; }
            protected set
            {
                _Error = value;
                if (value != null)
                {
                    _FinishTick = Environment.TickCount;
                    _RTT = _FinishTick - _StartTick;
                    Done = true;
                }
            }
        }

        protected int _RTT = -1;
        public int RTT { get { return _RTT; } }
        protected int _StartTick;
        public int StartTick { get { return _StartTick; } }
        protected int _FinishTick;

        protected int _Timeout = -1;
        public int Timeout { get { return _Timeout; } set { _Timeout = value; } }

        public Request()
        {
            _StartTick = Environment.TickCount;
        }
        public Request(object reqobj)
            : this()
        {
            _RequestObj = reqobj;
        }

        public abstract void Dispose();

        public delegate object Handler(IReqClient from, uint type, object reqobj, uint seq);
        public delegate object Handler<T>(IReqClient from, uint type, T reqobj, uint seq);

        public static Request Combine(Request main, Request inner)
        {
            Action OnInnerDone = null;
            OnInnerDone = () =>
            {
                inner.OnDone -= OnInnerDone;
                main.Error = inner.Error;
                main.ResponseObj= inner.ResponseObj;
            };
            inner.OnDone += OnInnerDone;
            return main;
        }
    }

    public class PeekedRequest : Request
    {
        protected internal PeekedRequest(IReqServer parent)
            : base()
        {
            _Parent = parent;
        }
        protected PeekedRequest(IReqServer parent, IReqClient from)
            : base()
        {
            _Parent = parent;
            _From = from;
        }
        protected PeekedRequest(IReqServer parent, IReqClient from, object reqobj)
            : base(reqobj)
        {
            _Parent = parent;
            _From = from;
        }
        public PeekedRequest(IReqServer parent, IReqClient from, object reqobj, uint seq)
            : base(reqobj)
        {
            _Parent = parent;
            _From = from;
            _Seq = seq;
        }

        #region Receive
        public virtual Type ReceiveType { get { return typeof(object); } }
        public virtual uint ReceiveRawType { get; set; }
        public virtual bool CanHandleRequest { get { return false; } }

        protected int _IsRequestReceived;
        public bool IsRequestReceived { get { return _IsRequestReceived != 0; } }
        protected internal bool SetRequest(object error, IReqClient from, object reqobj, uint seq)
        {
            if (Interlocked.CompareExchange(ref _IsRequestReceived, 1, 0) == 0)
            {
                if (error != null)
                {
                    SetError(error);
                }
                else
                {
                    _From = from;
                    _RequestObj = reqobj;
                    _Seq = seq;
                }
                OnRequestReceived();
                return true;
            }
            return false;
        }
        protected internal bool SetRequest(IReqClient from, object reqobj, uint seq)
        {
            return SetRequest(null, from, reqobj, seq);
        }
        protected internal bool SetReceiveError(object error)
        {
            return SetRequest(error, null, null, 0);
        }
        public event Action OnReceived = () => { };
        protected virtual void OnRequestReceived()
        {
            OnReceived();
        }

        public void StartReceive()
        {
            _StartTick = Environment.TickCount;
            CreateReceiveTrack(_Parent).Track(this);
        }
        public object TryReceive(IReqClient from, uint type, object reqobj, uint seq)
        {
            if (ReceiveRawType != 0 && ReceiveRawType == type)
            {
                if (SetRequest(from, reqobj, seq))
                {
                    return this;
                }
            }
            else if (reqobj != null && ReceiveType.IsAssignableFrom(reqobj.GetType()))
            {
                if (SetRequest(from, reqobj, seq))
                {
                    return this;
                }
            }
            return null;
        }
        public bool CheckReceiveTimeout()
        {
            if (_Timeout >= 0)
            { // check timeout
                if (Environment.TickCount >= _StartTick + _Timeout)
                {
                    SetReceiveError("timedout");
                    return true;
                }
            }
            return false;
        }

        protected class ReceiveTracker
        {
            protected IReqServer _Server;
            public IReqServer Server { get { return _Server; } }

            public ReceiveTracker(IReqServer server)
            {
                _Server = server;
                server.RegHandler(OnServerReceive);
                server.OnClose += OnServerClose;
            }
            protected void OnServerClose()
            {
                _Server.RemoveHandler(OnServerReceive);
                _Server.OnClose -= OnServerClose;
                RemoveReceiveTracker(_Server);
                _Server = null;

                PeekedRequest awaiter;
                while (_PendingAwaiters.TryDequeue(out awaiter))
                {
                    awaiter.SetReceiveError("connection closed");
                }
                foreach (var cawaiter in _CheckingAwaiters)
                {
                    cawaiter.SetReceiveError("connection closed");
                }
                _CheckingAwaiters.Clear();
            }

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
            protected ConcurrentQueueGrowOnly<PeekedRequest> _PendingAwaiters = new ConcurrentQueueGrowOnly<PeekedRequest>();
#else
            protected ConcurrentQueue<PeekedRequest> _PendingAwaiters = new ConcurrentQueue<PeekedRequest>();
#endif
            protected LinkedList<PeekedRequest> _CheckingAwaiters = new LinkedList<PeekedRequest>();
            public void Track(PeekedRequest awaiter)
            {
                _PendingAwaiters.Enqueue(awaiter);
            }
            [EventOrder(50)]
            protected object OnServerReceive(IReqClient from, uint type, object req, uint seq)
            {
                object received = null;
                LinkedListNode<PeekedRequest> node = _CheckingAwaiters.First;
                while (node != null)
                {
                    var next = node.Next;
                    var cawaiter = node.Value;
                    if (received == null)
                    {
                        received = cawaiter.TryReceive(from, type, req, seq);
                        if (received != null || cawaiter.CheckReceiveTimeout())
                        {
                            _CheckingAwaiters.Remove(node);
                        }
                        if (received != null && !cawaiter.CanHandleRequest)
                        {
                            received = null;
                        }
                    }
                    else
                    {
                        if (cawaiter.CheckReceiveTimeout())
                        {
                            _CheckingAwaiters.Remove(node);
                        }
                    }
                    node = next;
                }

                PeekedRequest awaiter;
                while (_PendingAwaiters.TryDequeue(out awaiter))
                {
                    if (received == null)
                    {
                        received = awaiter.TryReceive(from, type, req, seq);
                        if (received == null && !awaiter.CheckReceiveTimeout())
                        {
                            _CheckingAwaiters.AddLast(awaiter);
                        }
                        if (received != null && !awaiter.CanHandleRequest)
                        {
                            received = null;
                        }
                    }
                    else
                    {
                        if (!awaiter.CheckReceiveTimeout())
                        {
                            _CheckingAwaiters.AddLast(awaiter);
                        }
                    }
                }

                return received;
            }
        }

#if !UNITY_ENGINE && !UNITY_5_3_OR_NEWER || NET_4_6 || NET_STANDARD_2_0
        protected static System.Collections.Concurrent.ConcurrentDictionary<IReqServer, ReceiveTracker> _AsyncReceiveHandlers = new ConcurrentDictionary<IReqServer, ReceiveTracker>();
        protected static void RemoveReceiveTracker(IReqServer server)
        {
            ReceiveTracker handler;
            _AsyncReceiveHandlers.TryRemove(server, out handler);
        }
        protected static ReceiveTracker CreateReceiveTrack(IReqServer server)
        {
            ReceiveTracker handler;
            handler = _AsyncReceiveHandlers.GetOrAdd(server, s => new ReceiveTracker(s));
            return handler;
        }
#else
        protected static Dictionary<IReqServer, ReceiveTracker> _AsyncReceiveHandlers = new Dictionary<IReqServer, ReceiveTracker>();
        protected static void RemoveReceiveTracker(IReqServer server)
        {
            lock (_AsyncReceiveHandlers)
            {
                _AsyncReceiveHandlers.Remove(server);
            }
        }
        protected static ReceiveTracker CreateReceiveTrack(IReqServer server)
        {
            ReceiveTracker handler;
            lock (_AsyncReceiveHandlers)
            {
                if (!_AsyncReceiveHandlers.TryGetValue(server, out handler))
                {
                    _AsyncReceiveHandlers[server] = handler = new ReceiveTracker(server);
                }
            }
            return handler;
        }
#endif
#endregion

        public override void Dispose()
        {
            SetError("Server refused to process the request.");
        }

        public void SetResponse(object resp)
        {
            ResponseObj = resp;
            if (CanHandleRequest && Parent != null)
            {
                Parent.SendResponse(From, resp, Seq);
            }
        }
        public void SetError(object error)
        {
            Error = error;
            if (CanHandleRequest && Parent != null)
            {
                Parent.SendResponse(From, new PredefinedMessages.Error() { Message = error.ToString() }, Seq);
            }
        }

        protected IReqServer _Parent;
        protected IReqClient _From;
        protected uint _Seq;

        public IReqServer Parent { get { return _Parent; } }
        public IReqClient From { get { return _From; } }
        public uint Seq { get { return _Seq; } }
    }
    public class PeekedRequest<T> : PeekedRequest
    {
        protected internal PeekedRequest(IReqServer parent)
            : base(parent)
        { }
        protected PeekedRequest(IReqServer parent, IReqClient from)
            : base(parent, from)
        { }
        protected PeekedRequest(IReqServer parent, IReqClient from, T reqobj)
            : base(parent, from, reqobj)
        { }
        public PeekedRequest(IReqServer parent, IReqClient from, T reqobj, uint seq)
            : base(parent, from, reqobj, seq)
        { }

        public override Type ReceiveType { get { return typeof(T); } }

        public T Request
        {
            get
            {
                if (_RequestObj is T)
                {
                    return (T)_RequestObj;
                }
                return default(T);
            }
            protected set
            {
                _RequestObj = value;
            }
        }
    }
    public class ReceivedRequest : PeekedRequest
    {
        protected internal ReceivedRequest(IReqServer parent)
            : base(parent)
        { }
        protected ReceivedRequest(IReqServer parent, IReqClient from)
            : base(parent, from)
        { }
        protected ReceivedRequest(IReqServer parent, IReqClient from, object reqobj)
            : base(parent, from, reqobj)
        { }
        public ReceivedRequest(IReqServer parent, IReqClient from, object reqobj, uint seq)
            : base(parent, from, reqobj, seq)
        { }

        public override bool CanHandleRequest { get { return true; } }
    }
    public class ReceivedRequest<T> : PeekedRequest<T>
    {
        protected internal ReceivedRequest(IReqServer parent)
            : base(parent)
        { }
        protected ReceivedRequest(IReqServer parent, IReqClient from)
            : base(parent, from)
        { }
        protected ReceivedRequest(IReqServer parent, IReqClient from, T reqobj)
            : base(parent, from, reqobj)
        { }
        public ReceivedRequest(IReqServer parent, IReqClient from, T reqobj, uint seq)
            : base(parent, from, reqobj, seq)
        { }

        public override bool CanHandleRequest { get { return true; } }
    }

    public interface IReqClient : IChannel
    {
        Request Send(object reqobj, int timeout);
        int Timeout { get; set; } // TODO: this is the default timeout for Send Method.
    }
    public interface IReqServer : IChannel
    {
        void RegHandler(Request.Handler handler);
        void RegHandler(uint type, Request.Handler handler);
        void RegHandler<T>(Request.Handler<T> handler);
        void RegHandler(Request.Handler handler, int order);
        void RegHandler(uint type, Request.Handler handler, int order);
        void RegHandler<T>(Request.Handler<T> handler, int order);
        void RemoveHandler(Request.Handler handler);
        void RemoveHandler(uint type, Request.Handler handler);
        void RemoveHandler<T>(Request.Handler<T> handler);
        object HandleRequest(IReqClient from, uint type, object reqobj, uint seq);
        void SendRawResponse(IReqClient to, object response, uint seq_pingback);

        //event Request.Handler HandleCommonRequest;
        event Action<IReqClient> OnPrepareConnection;
    }

    public static class RequestExtensions
    {
        public static Request Send(this IReqClient client, object reqobj)
        {
            return client.Send(reqobj, -1);
        }
        public static void SendMessage(this IReqClient client, object reqobj) // send an object and donot track response
        {
            var req = client.Send(reqobj);
            if (req != null)
            {
                req.Dispose();
            }
        }
        public static void SendResponse(this IReqServer thiz, IReqClient to, object response, uint seq_pingback)
        {
            var resp = response as Request;
            if (resp != null)
            {
                if (resp is PeekedRequest)
                {
                    // the SendResponse it is handled by ReceivedRequest itself.
                }
                else
                {
                    resp.OnDone += () =>
                    {
                        thiz.SendRawResponse(to, resp.ResponseObj, seq_pingback);
                    };
                }
            }
            else if (response != null)
            {
                thiz.SendRawResponse(to, response, seq_pingback);
            }
            else
            {
                thiz.SendRawResponse(to, PredefinedMessages.Empty, seq_pingback); // we send an empty response.
            }
        }

        public interface IAwaiter : System.Runtime.CompilerServices.INotifyCompletion
        {
            bool IsCompleted { get; }
            void GetResult();
        }

        public class RequestAwaiter : IAwaiter
        {
            protected Request _Req;
            public RequestAwaiter(Request req)
            {
                _Req = req;
            }
            public bool IsCompleted { get { return _Req.Done; } }

            protected Action _CompleteContinuation;
            public void OnRequestDone()
            {
                _Req.OnDone -= OnRequestDone;
                if (_CompleteContinuation != null)
                {
                    _CompleteContinuation();
                }
            }
            public void OnCompleted(Action continuation)
            {
                if (_Req.Done)
                {
                    continuation();
                }
                else
                {
                    _CompleteContinuation = continuation;
                    _Req.OnDone += OnRequestDone;
                }
            }
            public void GetResult()
            {
                //// let outter caller handle the error.
                //if (_Req.Error != null)
                //{
                //    PlatDependant.LogError(_Req.Error);
                //}
            }
        }
        public static RequestAwaiter GetAwaiter(this Request req)
        {
            return new RequestAwaiter(req);
        }

        public class SynchronizationContextAwaiter : IAwaiter
        {
            protected SynchronizationContext _Context;
            public SynchronizationContextAwaiter(SynchronizationContext context)
            {
                _Context = context;
            }

            protected bool _IsCompleted;
            public bool IsCompleted { get { return _IsCompleted; } }
            public void OnCompleted(Action continuation)
            {
                if (SynchronizationContext.Current == _Context)
                {
                    _IsCompleted = true;
                    continuation();
                }
                else
                {
                    _Context.Post(state =>
                    {
                        _IsCompleted = true;
                        continuation();
                    }, null);
                }
            }
            public void GetResult()
            {
            }
        }
        public static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext req)
        {
            return new SynchronizationContextAwaiter(req);
        }

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        public class LegacyUnityAwaiter : IAwaiter
        {
            protected bool _IsCompleted;
            public bool IsCompleted { get { return _IsCompleted; } }
            public void OnCompleted(Action continuation)
            {
                if (ThreadSafeValues.IsMainThread)
                {
                    _IsCompleted = true;
                    continuation();
                }
                else
                {
                    UnityThreadDispatcher.RunInUnityThread(() =>
                    {
                        _IsCompleted = true;
                        continuation();
                    });
                }
            }
            public void GetResult()
            {
            }

            public LegacyUnityAwaiter GetAwaiter() { return this; }
        }
#endif

        public class DummyAwaiter : IAwaiter
        {
            protected bool _IsCompleted;
            public bool IsCompleted { get { return _IsCompleted; } }
            public void OnCompleted(Action continuation)
            {
                _IsCompleted = true;
                continuation();
            }
            public void GetResult()
            {
            }

            public DummyAwaiter GetAwaiter() { return this; }
        }

        public struct MainThreadAwaiter
        { // In the fact, we do not need this. If we await some task, the runtime will record SynchronizationContext.Current automatically.
            public bool ShouldWait { get; private set; }
            private IAwaiter _Awaiter;
            public void Init()
            {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
                if (ShouldWait = ThreadSafeValues.IsMainThread)
                {
#if UNITY_2017_1_OR_NEWER
                    _Awaiter = SynchronizationContext.Current.GetAwaiter();
#else
                    _Awaiter = new LegacyUnityAwaiter();
#endif
                }
#else
                ShouldWait = false;
#endif
            }

            public static MainThreadAwaiter Create()
            {
                var instance = new MainThreadAwaiter();
                instance.Init();
                return instance;
            }

            public IAwaiter GetAwaiter()
            {
                return _Awaiter;
            }
        }

        public static async System.Threading.Tasks.Task<object> SendAsync(this IReqClient client, object reqobj, int timeout)
        {
            var req = client.Send(reqobj, timeout);
            if (req == null)
            {
                return null;
            }
            await req;
            //// let outter caller handle the error.
            //if (req.Error != null)
            //{
            //    PlatDependant.LogError(req.Error);
            //}
            return req.ResponseObj;
        }
        public static async System.Threading.Tasks.Task<object> SendAsync(this IReqClient client, object reqobj)
        {
            return await SendAsync(client, reqobj, -1);
        }
        public static async System.Threading.Tasks.Task<T> SendAsync<T>(this IReqClient client, object reqobj, int timeout)
        {
            var req = client.Send(reqobj, timeout);
            if (req == null)
            {
                return default(T);
            }
            await req;
            //// let outter caller handle the error.
            //if (req.Error != null)
            //{
            //    PlatDependant.LogError(req.Error);
            //}
            if (typeof(T) == typeof(Request))
            {
                return (T)(object)req;
            }
            else
            {
                return req.GetResponse<T>();
            }
        }
        public static async System.Threading.Tasks.Task<T> SendAsync<T>(this IReqClient client, object reqobj)
        {
            return await SendAsync<T>(client, reqobj, -1);
        }

        public class TickAwaiter : IAwaiter
        {
            protected int _WaitToTick;
            public TickAwaiter(int waitInterval)
            {
                _WaitToTick = Environment.TickCount + waitInterval;
            }

            public bool IsCompleted { get { return Environment.TickCount >= _WaitToTick; } }

            protected Action _CompleteContinuation;
            public void OnCompleted(Action continuation)
            {
                if (IsCompleted)
                {
                    continuation();
                }
                else
                {
                    _CompleteContinuation = continuation;
                    StartCheck(this);
                }
            }
            public void GetResult()
            {
            }

            protected class TickAwaiterComparer : IComparer<TickAwaiter>
            {
                public int Compare(TickAwaiter x, TickAwaiter y)
                {
                    return y._WaitToTick - x._WaitToTick;
                }
            }
            protected static TickAwaiterComparer _Comparer = new TickAwaiterComparer();

            protected static AutoResetEvent _NewAwaiterGot = new AutoResetEvent(false);
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
            protected static ConcurrentQueueGrowOnly<TickAwaiter> _PendingAwaiters = new ConcurrentQueueGrowOnly<TickAwaiter>();
#else
            protected static ConcurrentQueue<TickAwaiter> _PendingAwaiters = new ConcurrentQueue<TickAwaiter>();
#endif
            protected static List<TickAwaiter> _CheckingAwaiters = new List<TickAwaiter>();
            protected static volatile bool _ChechkingStarted;
            protected static void CheckCompletion(TaskProgress prog)
            {
                try
                {
                    while (true)
                    {
                        TickAwaiter pending;
                        while (_PendingAwaiters.TryDequeue(out pending))
                        {
                            var index = _CheckingAwaiters.BinarySearch(pending, _Comparer);
                            if (index >= 0)
                            {
                                _CheckingAwaiters.Insert(index, pending);
                            }
                            else
                            {
                                _CheckingAwaiters.Insert(~index, pending);
                            }
                        }
                        for (int i = _CheckingAwaiters.Count - 1; i >= 0; --i)
                        {
                            var awaiter = _CheckingAwaiters[i];
                            if (awaiter._WaitToTick <= Environment.TickCount)
                            {
                                _CheckingAwaiters.RemoveAt(i);
                                awaiter._CompleteContinuation();
                            }
                        }
                        if (_CheckingAwaiters.Count == 0)
                        {
                            _NewAwaiterGot.WaitOne();
                        }
                        else
                        {
                            var last = _CheckingAwaiters[_CheckingAwaiters.Count - 1];
                            var waittick = last._WaitToTick - Environment.TickCount;
                            if (waittick < 0)
                            {
                                waittick = 0;
                            }
                            _NewAwaiterGot.WaitOne(waittick);
                        }
                    }
                }
                finally
                {
                    _ChechkingStarted = false;
                }
            }
            protected static void StartCheck(TickAwaiter awaiter)
            {
                _PendingAwaiters.Enqueue(awaiter);
                _NewAwaiterGot.Set();
                if (!_ChechkingStarted)
                {
                    _ChechkingStarted = true;
                    PlatDependant.RunBackgroundLongTime(CheckCompletion);
                }
            }

            public TickAwaiter GetAwaiter()
            {
                return this;
            }
        }
        public static async System.Threading.Tasks.Task WaitForTick(int tick)
        {
            await new TickAwaiter(tick);
        }

#region Receive
        public class ReceiveAwaiter : IAwaiter
        {
            protected PeekedRequest _Request;

            public ReceiveAwaiter(PeekedRequest req)
            {
                _Request = req;
                req.OnReceived += OnRequestReceived;
            }

            protected Action _CompleteContinuation;
            protected void OnRequestReceived()
            {
                _Request.OnReceived -= OnRequestReceived;
                if (_CompleteContinuation != null)
                {
                    _CompleteContinuation();
                }
            }

            public bool IsCompleted { get { return _Request.IsRequestReceived; } }
            public void GetResult()
            {
                ////let outter caller handle the error
                //if (Error != null)
                //{
                //    PlatDependant.LogError(Error);
                //}
            }

            public ReceiveAwaiter GetAwaiter() { return this; }

            public virtual void OnCompleted(Action continuation)
            {
                _CompleteContinuation = continuation;
                _Request.StartReceive();
            }
        }
        public static ReceiveAwaiter GetAwaiter(this PeekedRequest req)
        {
            return new ReceiveAwaiter(req);
        }

        // TODO: use async Enumerator to Receive a queue of Requests

        public static PeekedRequest Peek(this IReqServer server)
        {
            var req = new PeekedRequest(server);
            req.StartReceive();
            return req;
        }
        public static PeekedRequest Peek(this IReqServer server, int timeout)
        {
            var req = new PeekedRequest(server) { Timeout = timeout };
            req.StartReceive();
            return req;
        }
        public static PeekedRequest Peek(this IReqServer server, uint type)
        {
            var req = new PeekedRequest(server) { ReceiveRawType = type };
            req.StartReceive();
            return req;
        }
        public static PeekedRequest Peek(this IReqServer server, uint type, int timeout)
        {
            var req = new PeekedRequest(server) { Timeout = timeout, ReceiveRawType = type };
            req.StartReceive();
            return req;
        }
        public static PeekedRequest<T> Peek<T>(this IReqServer server)
        {
            var req = new PeekedRequest<T>(server);
            req.StartReceive();
            return req;
        }
        public static PeekedRequest<T> Peek<T>(this IReqServer server, int timeout)
        {
            var req = new PeekedRequest<T>(server) { Timeout = timeout };
            req.StartReceive();
            return req;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest<T>> PeekAsync<T>(this IReqServer server)
        {
            var request = new PeekedRequest<T>(server);
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest> PeekAsync(this IReqServer server)
        {
            var request = new PeekedRequest(server);
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest> PeekAsync(this IReqServer server, uint type)
        {
            var request = new PeekedRequest(server) { ReceiveRawType = type };
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest<T>> PeekAsync<T>(this IReqServer server, int timeout)
        {
            var request = new PeekedRequest<T>(server) { Timeout = timeout };
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest> PeekAsync(this IReqServer server, int timeout)
        {
            var request = new PeekedRequest(server) { Timeout = timeout };
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest> PeekAsync(this IReqServer server, uint type, int timeout)
        {
            var request = new PeekedRequest(server) { Timeout = timeout, ReceiveRawType = type };
            await request;
            return request;
        }
        public static ReceivedRequest Receive(this IReqServer server)
        {
            var req = new ReceivedRequest(server);
            req.StartReceive();
            return req;
        }
        public static ReceivedRequest Receive(this IReqServer server, int timeout)
        {
            var req = new ReceivedRequest(server) { Timeout = timeout };
            req.StartReceive();
            return req;
        }
        public static ReceivedRequest Receive(this IReqServer server, uint type)
        {
            var req = new ReceivedRequest(server) { ReceiveRawType = type };
            req.StartReceive();
            return req;
        }
        public static ReceivedRequest Receive(this IReqServer server, uint type, int timeout)
        {
            var req = new ReceivedRequest(server) { Timeout = timeout, ReceiveRawType = type };
            req.StartReceive();
            return req;
        }
        public static ReceivedRequest<T> Receive<T>(this IReqServer server)
        {
            var req = new ReceivedRequest<T>(server);
            req.StartReceive();
            return req;
        }
        public static ReceivedRequest<T> Receive<T>(this IReqServer server, int timeout)
        {
            var req = new ReceivedRequest<T>(server) { Timeout = timeout };
            req.StartReceive();
            return req;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest<T>> ReceiveAsync<T>(this IReqServer server)
        {
            var request = new ReceivedRequest<T>(server);
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest> ReceiveAsync(this IReqServer server)
        {
            var request = new ReceivedRequest(server);
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest> ReceiveAsync(this IReqServer server, uint type)
        {
            var request = new ReceivedRequest(server) { ReceiveRawType = type };
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest<T>> ReceiveAsync<T>(this IReqServer server, int timeout)
        {
            var request = new ReceivedRequest<T>(server) { Timeout = timeout };
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest> ReceiveAsync(this IReqServer server, int timeout)
        {
            var request = new ReceivedRequest(server) { Timeout = timeout };
            await request;
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest> ReceiveAsync(this IReqServer server, uint type, int timeout)
        {
            var request = new ReceivedRequest(server) { Timeout = timeout, ReceiveRawType = type };
            await request;
            return request;
        }
#endregion
    }

    public class BaseReqHandler
    {
        protected class HandleRequestEvent : OrderedEvent<Request.Handler>
        {
            public object CallHandlers(IReqClient from, uint type, object reqobj, uint seq)
            {
                for (int i = 0; i < _InvocationList.Count; ++i)
                {
                    var resp = _InvocationList[i].Handler(from, type, reqobj, seq);
                    if (resp != null)
                    {
                        return resp;
                    }
                }
                return null;
            }
            protected override void CombineHandlers()
            {
                _CachedCombined = CallHandlers;
            }

            protected Dictionary<Delegate, Request.Handler> _TypedHandlersMap = new Dictionary<Delegate, Request.Handler>();
            public void AddHandler<T>(Request.Handler<T> handler, int order)
            {
                Request.Handler converted;
                if (!_TypedHandlersMap.TryGetValue(handler, out converted))
                {
                    converted = (from, type, reqobj, seq) => handler(from, type, (T)reqobj, seq);
                    _TypedHandlersMap[handler] = converted;
                }
                AddHandler(converted, order);
            }
            public void AddHandler<T>(Request.Handler<T> handler)
            {
                AddHandler(handler, handler.GetOrder());
            }
            public void RemoveHandler<T>(Request.Handler<T> handler)
            {
                Request.Handler converted;
                if (_TypedHandlersMap.TryGetValue(handler, out converted))
                {
                    _TypedHandlersMap.Remove(handler);
                    RemoveHandler(converted);
                }
            }

            public HandleRequestEvent Clone()
            {
                var clone = new HandleRequestEvent();
                clone._InvocationList.AddRange(_InvocationList);
                return clone;
            }
        }

        protected Dictionary<Type, HandleRequestEvent> _TypedHandlers = new Dictionary<Type, HandleRequestEvent>();
        protected Dictionary<uint, HandleRequestEvent> _RawTypedHandlers = new Dictionary<uint, HandleRequestEvent>();
        protected HandleRequestEvent _CommonHandlers = new HandleRequestEvent();
        public void RegHandler(Request.Handler handler)
        {
            if (handler == null)
            {
                return;
            }
            lock (_CommonHandlers)
            {
                _CommonHandlers.AddHandler(handler);
            }
        }
        public void RegHandler(Request.Handler handler, int order)
        {
            if (handler == null)
            {
                return;
            }
            lock (_CommonHandlers)
            {
                _CommonHandlers.AddHandler(handler, order);
            }
        }
        public void RegHandler<T>(Request.Handler<T> handler)
        {
            if (handler == null)
            {
                return;
            }
            lock (_TypedHandlers)
            {
                var type = typeof(T);
                HandleRequestEvent list;
                if (!_TypedHandlers.TryGetValue(type, out list))
                {
                    list = new HandleRequestEvent();
                    _TypedHandlers[type] = list;
                }
                list.AddHandler(handler);
            }
        }
        public void RegHandler<T>(Request.Handler<T> handler, int order)
        {
            if (handler == null)
            {
                return;
            }
            lock (_TypedHandlers)
            {
                var type = typeof(T);
                HandleRequestEvent list;
                if (!_TypedHandlers.TryGetValue(type, out list))
                {
                    list = new HandleRequestEvent();
                    _TypedHandlers[type] = list;
                }
                list.AddHandler(handler, order);
            }
        }
        public void RegHandler(uint type, Request.Handler handler)
        {
            if (handler == null)
            {
                return;
            }
            if (type == 0)
            {
                RegHandler(handler);
            }
            else
            {
                lock (_RawTypedHandlers)
                {
                    HandleRequestEvent list;
                    if (!_RawTypedHandlers.TryGetValue(type, out list))
                    {
                        list = new HandleRequestEvent();
                        _RawTypedHandlers[type] = list;
                    }
                    list.AddHandler(handler);
                }
            }
        }
        public void RegHandler(uint type, Request.Handler handler, int order)
        {
            if (handler == null)
            {
                return;
            }
            if (type == 0)
            {
                RegHandler(handler, order);
            }
            else
            {
                lock (_RawTypedHandlers)
                {
                    HandleRequestEvent list;
                    if (!_RawTypedHandlers.TryGetValue(type, out list))
                    {
                        list = new HandleRequestEvent();
                        _RawTypedHandlers[type] = list;
                    }
                    list.AddHandler(handler, order);
                }
            }
        }
        public void RemoveHandler(Request.Handler handler)
        {
            if (handler == null)
            {
                lock (_CommonHandlers)
                {
                    _CommonHandlers.RemoveAll();
                }
            }
            else
            {
                lock (_CommonHandlers)
                {
                    _CommonHandlers.RemoveHandler(handler);
                }
            }
        }
        public void RemoveHandler(uint type, Request.Handler handler)
        {
            if (type == 0)
            {
                RemoveHandler(handler);
            }
            else
            {
                lock (_RawTypedHandlers)
                {
                    if (handler == null)
                    {
                        _RawTypedHandlers.Remove(type);
                    }
                    else
                    {
                        HandleRequestEvent list;
                        if (_RawTypedHandlers.TryGetValue(type, out list))
                        {
                            list.RemoveHandler(handler);
                        }
                    }
                }
            }
        }
        public void RemoveHandler<T>(Request.Handler<T> handler)
        {
            if (handler == null)
            {
                lock (_TypedHandlers)
                {
                    var type = typeof(T);
                    _TypedHandlers.Remove(type);
                }
            }
            else
            {
                lock (_TypedHandlers)
                {
                    var type = typeof(T);
                    HandleRequestEvent list;
                    if (_TypedHandlers.TryGetValue(type, out list))
                    {
                        list.RemoveHandler(handler);
                    }
                }
            }
        }
        [EventOrder(-100)]
        public object HandleRequest(IReqClient from, uint messagetype, object reqobj, uint seq)
        { // TODO: the handler merge should happen in RegHandler / RemoveHandler
            object respobj = null;
            Type type = null;
            if (reqobj != null)
            {
                type = reqobj.GetType();
            }
            HandleRequestEvent merged = null;
            if (type != null)
            {
                HandleRequestEvent list;
                lock (_TypedHandlers)
                {
                    _TypedHandlers.TryGetValue(type, out list);
                    if (list != null)
                    {
                        merged = list.Clone();
                    }
                }
            }
            {
                HandleRequestEvent list;
                lock (_RawTypedHandlers)
                {
                    _RawTypedHandlers.TryGetValue(messagetype, out list);
                    if (list != null)
                    {
                        if (merged == null)
                        {
                            merged = list.Clone();
                        }
                        else
                        {
                            merged.MergeHandlers(list);
                        }
                    }
                }
            }
            {
                HandleRequestEvent list;
                lock (_CommonHandlers)
                {
                    list = _CommonHandlers;
                    if (merged == null)
                    {
                        merged = list.Clone();
                    }
                    else
                    {
                        merged.MergeHandlers(list);
                    }
                }
            }
            respobj = merged.CallHandlers(from, messagetype, reqobj, seq);
            return respobj;
        }
    }

    public abstract class ReqHandler : BaseReqHandler, IReqServer
    {
        public abstract void SendRawResponse(IReqClient to, object response, uint seq_pingback);
        public abstract void Start();
        public abstract bool IsStarted { get; }
        public abstract bool IsAlive { get; }
        public abstract bool IsConnected { get; }
        public abstract SerializationConfig SerializationConfig { get; }
        public abstract Serializer Serializer { get; set; }

        public event Action OnClose = () => { };
        protected void FireOnClose() { OnClose(); }
        public event Action<IServerConnectionLifetime> OnConnected;
        protected virtual void FireOnConnected(IServerConnectionLifetime child)
        {
            if (OnConnected != null)
            {
                OnConnected(child);
            }
        }
        public event Action<IReqClient> OnPrepareConnection;
        protected void FireOnPrepareConnection(IReqClient child)
        {
            if (OnPrepareConnection != null)
            {
                OnPrepareConnection(child);
            }
        }
        public event Action OnUpdate;
        protected void FireOnUpdate()
        {
            if (OnUpdate != null)
            {
                OnUpdate();
            }
        }
#region IDisposable Support
        protected int _DisposedCnt = 0;
        protected bool _Disposed { get { return _DisposedCnt > 0; } }
        protected virtual void Dispose(bool disposing)
        {
            if (System.Threading.Interlocked.Increment(ref _DisposedCnt) == 1)
            {
                OnDispose();
                OnClose();
            }
        }
        protected abstract void OnDispose();
        ~ReqHandler()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
#endregion
    }

    public interface IReqConnection : IReqClient, IReqServer
    {
    }
    public class ReqClient : ReqHandler, IReqConnection, IPositiveConnection, IDisposable
    {
        protected ObjClient _Channel;
        public ObjClient Channel { get { return _Channel; } }
        public override SerializationConfig SerializationConfig { get { return _Channel.SerializationConfig; } }
        public override Serializer Serializer { get { return _Channel.Serializer; } set { _Channel.Serializer = value; } }
        protected bool _IsBackground = false;
        public bool IsBackground { get { return _IsBackground; } }

        public ReqClient(ObjClient channel, IDictionary<string, object> exconfig)
        {
            if (exconfig.Get<bool>("background"))
            {
                _IsBackground = true;
            }

            _Channel = channel;
            _Channel.OnReceiveObj += OnChannelReceive;
            _Channel.OnConnected += FireOnConnected;
            _Channel.OnUpdate += FireOnUpdate;
            _Channel.OnClose += Dispose;
        }
        public ReqClient(ObjClient channel)
            : this(channel, null)
        { }

        protected bool _IsConnected;
        public override bool IsConnected { get { return _IsConnected; } }
        protected override void FireOnConnected(IServerConnectionLifetime child)
        {
            _IsConnected = true;
            _Channel.OnConnected -= FireOnConnected;
            base.FireOnConnected(this);
        }

        protected class Request : Capstones.Net.Request
        {
            public Request(ReqClient parent)
                : base()
            {
                Parent = parent;
            }
            public Request(ReqClient parent, object reqobj)
                : base(reqobj)
            {
                Parent = parent;
            }

            public override void Dispose()
            {
                if (Seq != 0)
                {
                    Parent._DisposingReq.Enqueue(Seq);
                }
            }

            public void SetResponse(object resp)
            {
                ResponseObj = resp;
            }
            public void SetError(object error)
            {
                Error = error;
            }

            protected ReqClient Parent;
            public uint Seq;
        }

        protected const int _MaxCheckingReqCount = 1024;
        protected long _MinSeqInChecking = 1;
        protected long _MaxSeqInChecking = 0;
        protected Request[] _CheckingReq = new Request[_MaxCheckingReqCount];
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        protected ConcurrentQueueGrowOnly<Request> _PendingReq = new ConcurrentQueueGrowOnly<Request>();
        protected ConcurrentQueueGrowOnly<uint> _DisposingReq = new ConcurrentQueueGrowOnly<uint>();
#else
        protected ConcurrentQueue<Request> _PendingReq = new ConcurrentQueue<Request>();
        protected ConcurrentQueue<uint> _DisposingReq = new ConcurrentQueue<uint>();
#endif
        public Capstones.Net.Request Send(object reqobj, int timeout)
        {
            if (_Channel == null || !_Channel.IsAlive)
            {
                return null;
            }
            if (_Disposed)
            {
                return null;
            }
            var seq = _Channel.Write(reqobj);
            if (seq == 0)
            {
                return null;
            }
            var req = new Request(this, reqobj);
            req.Timeout = timeout;
            req.Seq = seq;
            _PendingReq.Enqueue(req);
            return req;
        }
        public override void SendRawResponse(IReqClient to, object response, uint seq_pingback)
        {
            if (_Channel != null && _Channel.IsAlive)
            {
                _Channel.Write(response, seq_pingback);
            }
        }
        protected int _Timeout = -1;
        public int Timeout { get { return _Timeout; } set { _Timeout = value; } }

        protected void ManagePendingRequests()
        {
            //1. add _pending
            Request pending;
            while (_PendingReq.TryDequeue(out pending))
            {
                var pseq = pending.Seq;
                if (pseq == 0)
                { // this req is being sent.
                    SpinWait spin = new SpinWait();
                    var spinStart = System.Environment.TickCount;
                    do
                    {
                        spin.SpinOnce();
                        pseq = pending.Seq;
                    } while (pseq == 0 && System.Environment.TickCount - spinStart < 2000);
                }
                if (pseq >= _MinSeqInChecking)
                {
                    var ncnt = pseq - _MinSeqInChecking + 1;
                    while (ncnt > _MaxCheckingReqCount)
                    {
                        var index = _MinSeqInChecking % _MaxCheckingReqCount;
                        var old = _CheckingReq[index];
                        if (old != null)
                        {
                            old.SetError("timedout - too many checking request");
                        }
                        _CheckingReq[index] = null;
                        ++_MinSeqInChecking;
                        --ncnt;
                    }

                    {
                        var index = pseq % _MaxCheckingReqCount;
                        _CheckingReq[index] = pending;
                        _MaxSeqInChecking = Math.Max(_MaxSeqInChecking, pseq);
                    }
                }
                else
                {
                    PlatDependant.LogError("Try to enqueue a req of " + pseq + ", min is " + _MinSeqInChecking);
                }
            }

            //2. delete disposing
            uint dispodingindex;
            while (_DisposingReq.TryDequeue(out dispodingindex))
            {
                if (dispodingindex >= _MinSeqInChecking && dispodingindex <= _MaxSeqInChecking)
                {
                    var index = dispodingindex % _MaxCheckingReqCount;
                    var old = _CheckingReq[index];
                    if (old != null)
                    {
                        old.SetError("canceled");
                    }
                    _CheckingReq[index] = null;
                    if (dispodingindex == _MinSeqInChecking)
                    {
                        ++_MinSeqInChecking;
                    }
                }
            }
        }
        protected void CheckRequestsTimeout()
        {
            var tick = Environment.TickCount;
            for (long i = _MinSeqInChecking; i <= _MaxSeqInChecking; ++i)
            {
                var index = i % _MaxCheckingReqCount;
                var checking = _CheckingReq[index];
                if (checking != null)
                {
                    var timeout = checking.Timeout;
                    if (timeout < 0)
                    {
                        timeout = _Timeout;
                    }
                    if (timeout >= 0)
                    {
                        if (tick - checking.StartTick >= timeout)
                        {
                            checking.SetError("timedout");
#if DEBUG_PVP
                            PlatDependant.LogError("timedout. seq: " + checking.Seq + "; timeout: " + checking.Timeout + "; start tick: " + checking.StartTick + "; current tick: " + tick + " of " +
                                (checking.RequestObj == null ? "null" : checking.RequestObj.GetType().Name));
#endif
                            _CheckingReq[index] = null;
                            if (i == _MinSeqInChecking)
                            {
                                ++_MinSeqInChecking;
                            }
                        }
                    }
                }
                else
                {
                    if (i == _MinSeqInChecking)
                    {
                        ++_MinSeqInChecking;
                    }
                }
            }
        }
        public void OnChannelReceive(object obj, uint type, uint seq, uint sseq)
        {
            // 1. add _pending; 2. delete disposing
            ManagePendingRequests();

            //3. check resp.
            uint pingback, reqseq;
            if (_Channel.IsServer)
            {
                pingback = sseq;
                reqseq = seq;
            }
            else
            {
                pingback = seq;
                reqseq = sseq;
            }
            bool isResponse = false;
            if (pingback != 0)
            {
                for (long i = _MinSeqInChecking; i <= _MaxSeqInChecking; ++i)
                {
                    var index = i % _MaxCheckingReqCount;
                    var checking = _CheckingReq[index];
                    if (checking != null)
                    {
                        if (checking.Seq == pingback)
                        {
                            isResponse = true;
                            checking.SetResponse(obj);
                        }
                        else if (checking.Seq < pingback)
                        { // the newer request is back, so we let older request timeout.
#if !DONOT_CHECK_OUT_OF_ORDER_TIMEOUT
                            checking.SetError("timedout - newer request is done");
#endif
#if DEBUG_PVP
                            PlatDependant.LogError("newer request is done. done: " + pingback + "; checking: " + checking.Seq + " of " +
                                (checking.RequestObj == null ? "null" : checking.RequestObj.GetType().Name));
#endif
                        }
                        else
                        {
                            break;
                        }
                    }
                    _CheckingReq[index] = null;
                    ++_MinSeqInChecking;
                }
            }
            if (!isResponse)
            { // this is not a response. this is a request from peer.
                var resp = HandleRequest(this, type, obj, reqseq);
                if (pingback == 0)
                {
                    this.SendResponse(this, resp, reqseq);
                }
            }

            //4. check timeout
            CheckRequestsTimeout();

            ////5 shrink - No need
            //if (maxSeq != 0)
            //{
            //    for (; _MinSeqInChecking < maxSeq; ++_MinSeqInChecking)
            //    {
            //        var index = _MinSeqInChecking % _MaxCheckingReqCount;
            //        if (_CheckingReq[index] != null)
            //        {
            //            break;
            //        }
            //    }
            //}

            //// we donot need the buffered obj, instead, we handle it directly in this callback. // outter caller will do this.
            //while (_Channel.TryRead() != null) ;
        }

        protected bool _Started;
        public override void Start()
        {
            if (_Started)
            {
                return;
            }
            FireOnPrepareConnection(this);
            _Channel.Start();
            _Started = true;
            if (!PositiveMode)
            {
                if (!_Channel.DeserializeInConnectionThread)
                {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
                    if (ThreadSafeValues.IsMainThread && !_IsBackground)
                    {
                        CoroutineRunner.StartCoroutine(RequestCheckWork());
                    }
                    else
#endif
                    {
                        PlatDependant.RunBackgroundLongTime(prog =>
                        {
                            try
                            {
                                while (!_Disposed && _Channel.IsAlive) _Channel.Read();
                            }
                            finally
                            {
                                OnDispose();
                            }
                        });
                    }
                }
            }
        }
        public override bool IsStarted { get { return _Started && (_Channel == null || _Channel.IsStarted); } }
        public override bool IsAlive { get { return !_Disposed && (!_Started || _Channel.IsAlive); } }

        public bool PositiveMode
        {
            get { return _Channel.PositiveMode; }
            set { _Channel.PositiveMode = value; }
        }
        /// <summary>
        /// use it in PositiveMode,
        /// or use it in ActiveMode - without Start, explicitly control when to check requests.
        /// </summary>
        public void Step()
        {
            if (_Disposed)
            {
                return;
            }
            if (!_Channel.IsAlive)
            {
                OnDispose();
                return;
            }
            if (PositiveMode)
            {
                _Channel.Step();
            }
            if (!_Started)
            {
                _Channel.Start();
                _Started = true;
            }
            while (_Channel.TryRead() != null) ;
        }
        public IEnumerator RequestCheckWork()
        {
            try
            {
                int checkTimeoutTick = Environment.TickCount;
                while (!_Disposed && _Channel.IsAlive)
                {
                    bool haveDataRead = false;
                    while (!_Disposed && _Channel.IsAlive && (_Channel.TryRead() != null))
                    {
                        haveDataRead = true;
                    }
                    if (haveDataRead)
                    {
                        checkTimeoutTick = Environment.TickCount;
                    }
                    else
                    {
                        var tick = Environment.TickCount;
                        if (tick - checkTimeoutTick > 1000)
                        {
                            ManagePendingRequests();
                            CheckRequestsTimeout();
                            checkTimeoutTick = tick;
                        }
                    }
                    yield return null;
                }
            }
            finally
            {
                OnDispose();
            }
        }

#region IDisposable Support
        public bool LeaveOpen = false;
        protected override void Dispose(bool disposing)
        {
            if (System.Threading.Interlocked.Increment(ref _DisposedCnt) == 1)
            {
                var isPositiveMode = PositiveMode;
                var channel = _Channel;
                if (channel != null)
                {
                    var con = channel.Connection;
                    if (con != null)
                    {
                        con.Dispose();
                    }
                    var stream = channel.Stream;
                    if (stream != null)
                    {
                        stream.Dispose();
                    }
                }
                if (isPositiveMode)
                {
                    OnDispose();
                }
            }
        }
        protected override void OnDispose()
        {
            System.Threading.Interlocked.Increment(ref _DisposedCnt);
            if (_Channel != null)
            {
                _Channel.OnReceiveObj -= OnChannelReceive;
                _Channel.OnConnected -= FireOnConnected;
                _Channel.OnUpdate -= FireOnUpdate;
                if (!LeaveOpen)
                {
                    _Channel.Dispose();
                }
                _Channel = null;
            }

            // fill all unfinished request to error
            HashSet<uint> disposingReq = new HashSet<uint>();
            uint dispodingindex;
            while (_DisposingReq.TryDequeue(out dispodingindex))
            {
                disposingReq.Add(dispodingindex);
            }
            Request pending;
            while (_PendingReq.TryDequeue(out pending))
            {
                var pseq = pending.Seq;
                if (!disposingReq.Contains(pseq))
                {
                    pending.SetError("connection closed.");
                }
            }
            for (int i = 0; i < _MaxCheckingReqCount; ++i)
            {
                pending = _CheckingReq[i];
                _CheckingReq[i] = null;
                if (pending != null)
                {
                    var pseq = pending.Seq;
                    if (!disposingReq.Contains(pseq))
                    {
                        pending.SetError("connection closed.");
                    }
                }
            }
            FireOnClose();
        }
#endregion
    }

    public class ReqServer : ReqHandler, IPositiveConnection, IDisposable
    {
        protected ObjServer _Server;
        public ObjServer Channel { get { return _Server; } }
        public override SerializationConfig SerializationConfig { get { return _Server.SerializationConfig; } }
        public override Serializer Serializer { get { return _Server.Serializer; } set { _Server.Serializer = value; } }
        protected IDictionary<string, object> _ExtraConfig;
        protected Request.Handler _ChildHandler;

        public ReqServer(ObjServer raw, IDictionary<string, object> exconfig)
        {
            _ExtraConfig = exconfig;
            _Server = raw;
            _Server.OnUpdate += FireOnUpdate;
            _PositiveConnection = raw as IPositiveConnection;
            _ChildHandler = HandleRequest;
        }
        public ReqServer(ObjServer raw)
            : this(raw, null)
        { }

        protected bool _Started;
        public override void Start()
        {
            if (!_Started)
            {
                _Server.Start();
                _Started = true;
            }
        }
        public override bool IsStarted { get { return _Started; } }
        public override bool IsAlive { get { return !_Disposed && (!_Started || _Server.IsAlive); } }
        public override bool IsConnected { get { return _Started; } }

        public IReqConnection GetConnection()
        {
            var channel = _Server.GetConnection();
            var child = new ReqClient(channel, _ExtraConfig);
            FireOnPrepareConnection(child);
            child.OnConnected += FireOnConnected;
            child.RegHandler(_ChildHandler);
            child.Start();
            return child;
        }
        protected override void FireOnConnected(IServerConnectionLifetime child)
        {
            child.OnConnected -= FireOnConnected;
            base.FireOnConnected(child);
        }

        public override void SendRawResponse(IReqClient to, object response, uint seq_pingback)
        {
            var client = to as IReqConnection;
            if (client != null && client.IsAlive)
            {
                client.SendRawResponse(to, response, seq_pingback);
            }
        }

#region IDisposable Support
        public bool LeaveOpen = false;
        protected override void OnDispose()
        {
            _Server.OnUpdate -= FireOnUpdate;
            if (!LeaveOpen)
            {
                if (_Server is IDisposable)
                {
                    ((IDisposable)_Server).Dispose();
                }
            }
            _Server = null;
        }
#endregion

#region IPositiveConnection
        protected IPositiveConnection _PositiveConnection;
        public bool PositiveMode
        {
            get
            {
                if (_PositiveConnection != null)
                {
                    return _PositiveConnection.PositiveMode;
                }
                return false;
            }
            set
            {
                if (_PositiveConnection != null)
                {
                    _PositiveConnection.PositiveMode = value;
                }
            }
        }
        public void Step()
        {
            if (_PositiveConnection != null)
            {
                _PositiveConnection.Step();
            }
        }
#endregion
    }

    public static class UriUtilities
    {
        public static Dictionary<string, object> ParseExtraConfigFromQuery(this Uri uri)
        {
            var querystr = uri.Query;
            if (!string.IsNullOrEmpty(querystr))
            {
                if (querystr.StartsWith("?"))
                {
                    querystr = querystr.Substring(1);
                }
                var querys = querystr.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                if (querys != null && querys.Length > 0)
                {
                    Dictionary<string, object> config = new Dictionary<string, object>();
                    foreach (var query in querys)
                    {
                        var index = query.IndexOf("=");
                        if (index < 0)
                        {
                            config[query] = true;
                        }
                        else
                        {
                            var key = query.Substring(0, index);
                            var value = query.Substring(index + 1);
                            config[key] = value;
                        }
                    }
                    return config;
                }
            }
            return null;
        }
        public static Dictionary<string, object> ParseExtraConfigFromQuery(string url)
        {
            return ParseExtraConfigFromQuery(new Uri(url));
        }
    }

    public static partial class ConnectionFactory
    {
        public interface IPersistentConnectionCreator
        {
            IPersistentConnection CreateClient(Uri uri, IDictionary<string, object> exconfig);
            IPersistentConnectionServer CreateServer(Uri uri, IDictionary<string, object> exconfig);
        }
        private static Dictionary<string, IPersistentConnectionCreator> _Creators;
        private static Dictionary<string, IPersistentConnectionCreator> Creators
        {
            get
            {
                if (_Creators == null)
                {
                    _Creators = new Dictionary<string, IPersistentConnectionCreator>();
                }
                return _Creators;
            }
        }
        public class RegisteredCreator : IPersistentConnectionCreator
        {
            public Func<Uri, IDictionary<string, object>, IPersistentConnection> ClientFactory;
            public Func<Uri, IDictionary<string, object>, IPersistentConnectionServer> ServerFactory;

            public RegisteredCreator(string scheme, Func<Uri, IDictionary<string, object>, IPersistentConnection> clientFactory, Func<Uri, IDictionary<string, object>, IPersistentConnectionServer> serverFactory)
            {
                ClientFactory = clientFactory;
                ServerFactory = serverFactory;
                Creators[scheme] = this;
            }
            public RegisteredCreator(string scheme, Func<Uri, IPersistentConnection> clientFactory, Func<Uri, IPersistentConnectionServer> serverFactory)
            {
                ClientFactory = (uri, exconfig) => clientFactory(uri);
                ServerFactory = (uri, exconfig) => serverFactory(uri);
                Creators[scheme] = this;
            }

            public IPersistentConnection CreateClient(Uri uri, IDictionary<string, object> exconfig)
            {
                return ClientFactory(uri, exconfig);
            }
            public IPersistentConnectionServer CreateServer(Uri uri, IDictionary<string, object> exconfig)
            {
                return ServerFactory(uri, exconfig);
            }
        }

        public interface IHighLevelCreator
        {
            IReqClient CreateClient(Uri uri, IDictionary<string, object> exconfig);
            IReqServer CreateServer(Uri uri, IDictionary<string, object> exconfig);
        }
        private static Dictionary<string, IHighLevelCreator> _HighLevelCreators;
        private static Dictionary<string, IHighLevelCreator> HighLevelCreators
        {
            get
            {
                if (_HighLevelCreators == null)
                {
                    _HighLevelCreators = new Dictionary<string, IHighLevelCreator>();
                }
                return _HighLevelCreators;
            }
        }
        public class HighLevelCreator : IHighLevelCreator
        {
            public Func<Uri, IDictionary<string, object>, IReqClient> ClientFactory;
            public Func<Uri, IDictionary<string, object>, IReqServer> ServerFactory;

            public HighLevelCreator(string scheme, Func<Uri, IDictionary<string, object>, IReqClient> clientFactory, Func<Uri, IDictionary<string, object>, IReqServer> serverFactory)
            {
                ClientFactory = clientFactory;
                ServerFactory = serverFactory;
                HighLevelCreators[scheme] = this;
            }
            public HighLevelCreator(string scheme, Func<Uri, IReqClient> clientFactory, Func<Uri, IReqServer> serverFactory)
            {
                ClientFactory = (uri, exconfig) => clientFactory(uri);
                ServerFactory = (uri, exconfig) => serverFactory(uri);
                HighLevelCreators[scheme] = this;
            }

            public IReqClient CreateClient(Uri uri, IDictionary<string, object> exconfig)
            {
                return ClientFactory(uri, exconfig);
            }
            public IReqServer CreateServer(Uri uri, IDictionary<string, object> exconfig)
            {
                return ServerFactory(uri, exconfig);
            }
        }


        private static SerializationConfig _DefaultSerializationConfig = null;
        public static SerializationConfig DefaultSerializationConfig
        {
            get { return _DefaultSerializationConfig ?? SerializationConfig.Default; }
            set { _DefaultSerializationConfig = value; }
        }

        public interface IClientAttachmentCreator : AttachmentExtensions.IAttachmentCreator<IReqClient>
        {
        }
        public interface IServerAttachmentCreator : AttachmentExtensions.IAttachmentCreator<IReqServer>
        {
        }
        public class ClientAttachmentCreator : IClientAttachmentCreator
        {
            protected string _Name;
            public string Name { get { return _Name; } }
            protected Func<IReqClient, object> _Creator;
            public object CreateAttachment(IReqClient client)
            {
                if (_Creator != null && client != null)
                {
                    return _Creator(client);
                }
                return null;
            }
            public object CreateAttachment(object owner)
            {
                return CreateAttachment(owner as IReqClient);
            }

            public ClientAttachmentCreator(string name, Func<IReqClient, object> creator)
            {
                _Name = name;
                _Creator = creator;
            }
        }
        public class ServerAttachmentCreator : IServerAttachmentCreator
        {
            protected string _Name;
            public string Name { get { return _Name; } }
            protected Func<IReqServer, object> _Creator;
            public object CreateAttachment(IReqServer server)
            {
                if (_Creator != null && server != null)
                {
                    return _Creator(server);
                }
                return null;
            }
            public object CreateAttachment(object owner)
            {
                return CreateAttachment(owner as IReqServer);
            }

            public ServerAttachmentCreator(string name, Func<IReqServer, object> creator)
            {
                _Name = name;
                _Creator = creator;
            }
        }
        public class CombinedAttachmentCreator<T> : AttachmentExtensions.IAttachmentCreator<IReqClient>, AttachmentExtensions.IAttachmentCreator<IReqServer>
            where T : class, IReqClient, IReqServer
        {
            protected string _Name;
            public string Name { get { return _Name; } }
            protected Func<T, object> _Creator;

            public CombinedAttachmentCreator(string name, Func<T, object> creator)
            {
                _Name = name;
                _Creator = creator;
            }

            public object CreateAttachment(T owner)
            {
                if (_Creator != null && owner != null)
                {
                    return _Creator(owner);
                }
                return null;
            }
            public object CreateAttachment(object owner)
            {
                return CreateAttachment(owner as T);
            }
            public object CreateAttachment(IReqClient owner)
            {
                return CreateAttachment(owner as T);
            }
            public object CreateAttachment(IReqServer owner)
            {
                return CreateAttachment(owner as T);
            }
        }

        public struct ConnectionConfig : IEnumerable
        {
            public SerializationConfig SConfig;
            public IDictionary<string, object> ExConfig;
            public IList<IClientAttachmentCreator> ClientAttachmentCreators;
            public IList<IServerAttachmentCreator> ServerAttachmentCreators;

            public IEnumerator GetEnumerator()
            {
                yield return SConfig;
                foreach (var creator in ClientAttachmentCreators)
                {
                    yield return creator;
                }
                foreach (var creator in ServerAttachmentCreators)
                {
                    yield return creator;
                }
            }

            public ConnectionConfig(ConnectionConfig other)
            {
                SConfig = other.SConfig;
                ExConfig = other.ExConfig;
                ClientAttachmentCreators = other.ClientAttachmentCreators == null ? null : new List<IClientAttachmentCreator>(other.ClientAttachmentCreators);
                ServerAttachmentCreators = other.ServerAttachmentCreators == null ? null : new List<IServerAttachmentCreator>(other.ServerAttachmentCreators);
            }

            public void Add(SerializationConfig sconfig)
            {
                SConfig = sconfig;
            }
            public void Add(IClientAttachmentCreator creator)
            {
                if (ClientAttachmentCreators == null)
                {
                    ClientAttachmentCreators = new List<IClientAttachmentCreator>();
                }
                ClientAttachmentCreators.Add(creator);
            }
            public void Add(IServerAttachmentCreator creator)
            {
                if (ServerAttachmentCreators == null)
                {
                    ServerAttachmentCreators = new List<IServerAttachmentCreator>();
                }
                ServerAttachmentCreators.Add(creator);
            }
        }

        public static IReqClient GetClient(string url, ConnectionConfig econfig)
        {
            var uri = new Uri(url);
            var scheme = uri.Scheme;
            var sconfig = econfig.SConfig ?? DefaultSerializationConfig;
            var acclient = econfig.ClientAttachmentCreators;
            //var acserver = econfig.ServerAttachmentCreators;
            IDictionary<string, object> exconfig = UriUtilities.ParseExtraConfigFromQuery(uri);
            exconfig = exconfig.Merge(econfig.ExConfig);

            IReqClient client = null;

            IHighLevelCreator hcreator;
            if (HighLevelCreators.TryGetValue(scheme, out hcreator))
            {
                client = hcreator.CreateClient(uri, exconfig);
            }
            if (client == null)
            {
                IPersistentConnectionCreator creator;
                if (Creators.TryGetValue(scheme, out creator))
                {
                    var connection = creator.CreateClient(uri, exconfig);
                    var channel = new ObjClient(connection, sconfig, exconfig);
                    client = new ReqClient(channel, exconfig);
                }
            }

            if (client != null)
            {
                if (acclient != null)
                {
                    for (int i = 0; i < acclient.Count; ++i)
                    {
                        var ac = acclient[i];
                        if (ac != null)
                        {
                            var attach = ac.CreateAttachment(client);
                            if (attach != null)
                            {
                                client.SetAttachment(ac.Name, attach);
                            }
                        }
                    }
                }
                
                if (!exconfig.Get<bool>("delaystart"))
                {
                    client.Start();
                }
            }

            return client;
        }
        public static IReqClient GetClient(string url, SerializationConfig sconfig)
        {
            return GetClient(url, new ConnectionConfig() { SConfig = sconfig });
        }
        public static IReqClient GetClient(string url)
        {
            return GetClient(url, null);
        }
        public static T GetClient<T>(string url, ConnectionConfig econfig) where T : class, IReqClient
        {
            return GetClient(url, econfig) as T;
        }
        public static T GetClient<T>(string url, SerializationConfig sconfig) where T : class, IReqClient
        {
            return GetClient(url, sconfig) as T;
        }
        public static T GetClient<T>(string url) where T : class, IReqClient
        {
            return GetClient(url) as T;
        }
        public static IReqServer GetServer(string url, ConnectionConfig econfig)
        {
            var uri = new Uri(url);
            var scheme = uri.Scheme;
            var sconfig = econfig.SConfig ?? DefaultSerializationConfig;
            var acclient = econfig.ClientAttachmentCreators;
            var acserver = econfig.ServerAttachmentCreators;
            var exconfig = UriUtilities.ParseExtraConfigFromQuery(uri);

            IReqServer server = null;

            IHighLevelCreator hcreator;
            if (HighLevelCreators.TryGetValue(scheme, out hcreator))
            {
                server = hcreator.CreateServer(uri, exconfig);
            }
            if (server == null)
            {
                IPersistentConnectionCreator creator;
                if (Creators.TryGetValue(scheme, out creator))
                {
                    var connection = creator.CreateServer(uri, exconfig);
                    var channel = new ObjServer(connection, sconfig, exconfig);
                    server = new ReqServer(channel, exconfig);
                }
            }

            if (server != null)
            {
                if (acserver != null)
                {
                    for (int i = 0; i < acserver.Count; ++i)
                    {
                        var ac = acserver[i];
                        if (ac != null)
                        {
                            var attach = ac.CreateAttachment(server);
                            if (attach != null)
                            {
                                server.SetAttachment(ac.Name, attach);
                            }
                        }
                    }
                }
                if (acclient != null)
                {
                    server.OnConnected += child =>
                    {
                        var client = child as IReqClient;
                        if (client != null)
                        {
                            for (int i = 0; i < acclient.Count; ++i)
                            {
                                var ac = acclient[i];
                                if (ac != null)
                                {
                                    var attach = ac.CreateAttachment(client);
                                    if (attach != null)
                                    {
                                        client.SetAttachment(ac.Name, attach);
                                    }
                                }
                            }
                        }
                    };
                }
                
                if (!exconfig.Get<bool>("delaystart"))
                {
                    server.Start();
                }
            }
            return server;
        }
        public static IReqServer GetServer(string url, SerializationConfig sconfig)
        {
            return GetServer(url, new ConnectionConfig() { SConfig = sconfig });
        }
        public static IReqServer GetServer(string url)
        {
            return GetServer(url, null);
        }
        public static T GetServer<T>(string url, ConnectionConfig econfig) where T : class, IReqServer
        {
            return GetServer(url, econfig) as T;
        }
        public static T GetServer<T>(string url, SerializationConfig sconfig) where T : class, IReqServer
        {
            return GetServer(url, sconfig) as T;
        }
        public static T GetServer<T>(string url) where T : class, IReqServer
        {
            return GetServer(url) as T;
        }
    }
}
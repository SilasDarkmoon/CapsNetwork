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

        public delegate object Handler(IReqClient from, object reqobj, uint seq);
        public delegate object Handler<T>(IReqClient from, T reqobj, uint seq);
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
        public object TryReceive(IReqClient from, object reqobj, uint seq)
        {
            if (reqobj != null && ReceiveType.IsAssignableFrom(reqobj.GetType()))
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

            protected ConcurrentQueueGrowOnly<PeekedRequest> _PendingAwaiters = new ConcurrentQueueGrowOnly<PeekedRequest>();
            protected LinkedList<PeekedRequest> _CheckingAwaiters = new LinkedList<PeekedRequest>();
            public void Track(PeekedRequest awaiter)
            {
                _PendingAwaiters.Enqueue(awaiter);
            }
            [EventOrder(50)]
            protected object OnServerReceive(IReqClient from, object req, uint seq)
            {
                object received = null;
                LinkedListNode<PeekedRequest> node = _CheckingAwaiters.First;
                while (node != null)
                {
                    var next = node.Next;
                    var cawaiter = node.Value;
                    if (received == null)
                    {
                        received = cawaiter.TryReceive(from, req, seq);
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
                        received = awaiter.TryReceive(from, req, seq);
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
        int Timeout { get; set; }
    }
    public interface IReqServer : IChannel
    {
        void RegHandler(Request.Handler handler);
        void RegHandler<T>(Request.Handler<T> handler);
        void RemoveHandler(Request.Handler handler);
        void RemoveHandler<T>(Request.Handler<T> handler);
        void HandleRequest(IReqClient from, object reqobj, uint seq);
        void SendResponse(IReqClient to, object response, uint seq_pingback);

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
        {
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
            var mtawaiter = MainThreadAwaiter.Create();
            var req = client.Send(reqobj, timeout);
            if (req == null)
            {
                return null;
            }
            await req;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return req.ResponseObj;
        }
        public static async System.Threading.Tasks.Task<object> SendAsync(this IReqClient client, object reqobj)
        {
            return await SendAsync(client, reqobj, -1);
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
            protected static ConcurrentQueueGrowOnly<TickAwaiter> _PendingAwaiters = new ConcurrentQueueGrowOnly<TickAwaiter>();
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
                    PlatDependant.RunBackground(CheckCompletion);
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
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new PeekedRequest<T>(server);
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest> PeekAsync(this IReqServer server)
        {
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new PeekedRequest(server);
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest<T>> PeekAsync<T>(this IReqServer server, int timeout)
        {
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new PeekedRequest<T>(server) { Timeout = timeout };
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return request;
        }
        public static async System.Threading.Tasks.Task<PeekedRequest> PeekAsync(this IReqServer server, int timeout)
        {
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new PeekedRequest(server) { Timeout = timeout };
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
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
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new ReceivedRequest<T>(server);
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest> ReceiveAsync(this IReqServer server)
        {
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new ReceivedRequest(server);
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest<T>> ReceiveAsync<T>(this IReqServer server, int timeout)
        {
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new ReceivedRequest<T>(server) { Timeout = timeout };
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return request;
        }
        public static async System.Threading.Tasks.Task<ReceivedRequest> ReceiveAsync(this IReqServer server, int timeout)
        {
            var mtawaiter = MainThreadAwaiter.Create();
            var request = new ReceivedRequest(server) { Timeout = timeout };
            await request;
            if (mtawaiter.ShouldWait)
            {
                await mtawaiter;
            }
            return request;
        }
        #endregion
    }

    public abstract class ReqHandler : IReqServer
    {
        protected class HandleRequestEvent : OrderedEvent<Request.Handler>
        {
            public object CallHandlers(IReqClient from, object reqobj, uint seq)
            {
                for (int i = 0; i < _InvocationList.Count; ++i)
                {
                    var resp = _InvocationList[i].Handler(from, reqobj, seq);
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
                    converted = (from, reqobj, seq) => handler(from, (T)reqobj, seq);
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
        protected HandleRequestEvent _CommonHandlers = new HandleRequestEvent();
        public void RegHandler(Request.Handler handler)
        {
            lock (_CommonHandlers)
            {
                _CommonHandlers.AddHandler(handler);
            }
        }
        public void RegHandler<T>(Request.Handler<T> handler)
        {
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
        public void RemoveHandler(Request.Handler handler)
        {
            lock (_CommonHandlers)
            {
                _CommonHandlers.RemoveHandler(handler);
            }
        }
        public void RemoveHandler<T>(Request.Handler<T> handler)
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
        public void HandleRequest(IReqClient from, object reqobj, uint seq)
        {
            object respobj = null;
            Type type = null;
            if (reqobj != null)
            {
                type = reqobj.GetType();
            }
            if (type != null)
            {
                HandleRequestEvent list;
                lock (_TypedHandlers)
                {
                    _TypedHandlers.TryGetValue(type, out list);
                    if (list != null)
                    {
                        list = list.Clone();
                    }
                }
                if (list != null)
                {
                    respobj = list.CallHandlers(from, reqobj, seq);
                }
            }
            if (respobj == null)
            {
                HandleRequestEvent list;
                lock (_CommonHandlers)
                {
                    list = _CommonHandlers.Clone();
                }
                respobj = list.CallHandlers(from, reqobj, seq);
            }
            var resp = respobj as Request;
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
                        SendResponse(from, resp.ResponseObj, seq);
                    };
                }
            }
            else if (respobj != null)
            {
                SendResponse(from, respobj, seq);
            }
            else
            {
                SendResponse(from, new PredefinedMessages.Raw(), seq); // we send an empty response.
            }
        }
        public abstract void SendResponse(IReqClient to, object response, uint seq_pingback);
        public abstract void Start();
        public abstract bool IsStarted { get; }
        public abstract bool IsAlive { get; }

        public event Action OnClose = () => { };
        public event Action<IChannel> OnConnected;
        protected virtual void FireOnConnected(IChannel child)
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
        protected bool _Disposed = false;
        protected void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                _Disposed = true;
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

    public class ReqClient : ReqHandler, IReqClient, IPositiveConnection, IDisposable
    {
        protected ObjClient _Channel;
        public ObjClient Channel { get { return _Channel; } }

        public ReqClient(ObjClient channel, IDictionary<string, object> exconfig)
        {
            _Channel = channel;
            _Channel.OnReceiveObj += OnChannelReceive;
            _Channel.OnConnected += FireOnConnected;
            _Channel.OnUpdate += FireOnUpdate;
        }
        public ReqClient(ObjClient channel)
            : this(channel, null)
        { }

        protected override void FireOnConnected(IChannel child)
        {
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
        protected int _MinSeqInChecking = 0;
        protected Request[] _CheckingReq = new Request[_MaxCheckingReqCount];
        protected ConcurrentQueueGrowOnly<Request> _PendingReq = new ConcurrentQueueGrowOnly<Request>();
        protected ConcurrentQueueGrowOnly<uint> _DisposingReq = new ConcurrentQueueGrowOnly<uint>();
        public Capstones.Net.Request Send(object reqobj, int timeout)
        {
            if (_Channel == null || !_Channel.IsAlive)
            {
                return null;
            }
            var req = new Request(this, reqobj);
            req.Timeout = timeout;
            _PendingReq.Enqueue(req);
            req.Seq = _Channel.Write(reqobj);
            return req;
        }
        public override void SendResponse(IReqClient to, object response, uint seq_pingback)
        {
            if (_Channel != null)
            {
                _Channel.Write(response, seq_pingback);
            }
        }
        protected int _Timeout = -1;
        public int Timeout { get { return _Timeout; } set { _Timeout = value; } }

        public void OnChannelReceive(object obj, uint type, uint seq, uint sseq)
        {
            //1. add _pending
            Request pending;
            uint maxSeq = 0;
            while (_PendingReq.TryDequeue(out pending))
            {
                var pseq = maxSeq = pending.Seq;
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
                }
            }

            //2. check resp.
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
            if (pingback != 0)
            {
                for (int i = 0; i < _MaxCheckingReqCount; ++i)
                {
                    var index = (_MinSeqInChecking + i) % _MaxCheckingReqCount;
                    var checking = _CheckingReq[index];
                    if (checking != null && checking.Seq != 0)
                    {
                        if (checking.Seq == pingback)
                        {
                            checking.SetResponse(obj);
                            _CheckingReq[index] = null;
                        }
                        else if (checking.Seq < pingback)
                        { // the newer request is back, so we let older request timeout.
                            checking.SetError("timedout - newer request is done");
                            _CheckingReq[index] = null;
                        }
                    }
                }
            }
            else
            { // this is not a response. this is a request from peer.
                HandleRequest(this, obj, reqseq);
            }

            //3. delete disposing
            uint dispodingindex;
            while (_DisposingReq.TryDequeue(out dispodingindex))
            {
                if (dispodingindex >= _MinSeqInChecking && dispodingindex < _MinSeqInChecking + _MaxCheckingReqCount)
                {
                    var index = dispodingindex % _MaxCheckingReqCount;
                    var old = _CheckingReq[index];
                    if (old != null)
                    {
                        old.SetError("canceled");
                    }
                    _CheckingReq[index] = null;
                }
            }

            //4. check timeout
            var tick = Environment.TickCount;
            for (int i = 0; i < _MaxCheckingReqCount; ++i)
            {
                var index = (_MinSeqInChecking + i) % _MaxCheckingReqCount;
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
                            _CheckingReq[index] = null;
                        }
                    }
                }
            }

            //5 shrink
            if (maxSeq != 0)
            {
                for (; _MinSeqInChecking < maxSeq; ++_MinSeqInChecking)
                {
                    var index = _MinSeqInChecking % _MaxCheckingReqCount;
                    if (_CheckingReq[index] != null)
                    {
                        break;
                    }
                }
            }

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
                    if (ThreadSafeValues.IsMainThread)
                    {
                        CoroutineRunner.StartCoroutine(RequestCheckWork());
                    }
                    else
#endif
                    {
                        PlatDependant.RunBackground(prog =>
                        {
                            try
                            {
                                while (!_Disposed) _Channel.Read();
                            }
                            finally
                            {
                                Dispose();
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
                Dispose();
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
                while (!_Disposed && _Channel.IsAlive)
                {
                    while (_Channel.TryRead() != null) ;
                    yield return null;
                }
            }
            finally
            {
                Dispose();
            }
        }

        #region IDisposable Support
        public bool LeaveOpen = false;
        protected override void OnDispose()
        {
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
        }
        #endregion
    }

    public class ReqServer : ReqHandler, IPositiveConnection, IDisposable
    {
        protected ObjServer _Server;
        public ObjServer Channel { get { return _Server; } }
        protected IDictionary<string, object> _ExtraConfig;
        protected Request.Handler _ChildHandler;

        public ReqServer(ObjServer raw, IDictionary<string, object> exconfig)
        {
            _ExtraConfig = exconfig;
            _Server = raw;
            _Server.OnUpdate += FireOnUpdate;
            _PositiveConnection = raw as IPositiveConnection;
            _ChildHandler = (from, reqobj, seq) => { HandleRequest(from, reqobj, seq); return null; };
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

        public ReqClient GetConnection()
        {
            var channel = _Server.GetConnection();
            var child = new ReqClient(channel, _ExtraConfig);
            FireOnPrepareConnection(child);
            child.OnConnected += FireOnConnected;
            child.RegHandler(_ChildHandler);
            child.Start();
            return child;
        }
        protected override void FireOnConnected(IChannel child)
        {
            child.OnConnected -= FireOnConnected;
            base.FireOnConnected(child);
        }

        public override void SendResponse(IReqClient to, object response, uint seq_pingback)
        {
            var client = to as ReqClient;
            if (client != null)
            {
                client.SendResponse(to, response, seq_pingback);
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
            IPersistentConnection CreateClient(Uri uri);
            IPersistentConnectionServer CreateServer(Uri uri);
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
            public Func<Uri, IPersistentConnection> ClientFactory;
            public Func<Uri, IPersistentConnectionServer> ServerFactory;

            public RegisteredCreator(string scheme, Func<Uri, IPersistentConnection> clientFactory, Func<Uri, IPersistentConnectionServer> serverFactory)
            {
                ClientFactory = clientFactory;
                ServerFactory = serverFactory;
                Creators[scheme] = this;
            }

            public IPersistentConnection CreateClient(Uri uri)
            {
                return ClientFactory(uri);
            }
            public IPersistentConnectionServer CreateServer(Uri uri)
            {
                return ServerFactory(uri);
            }
        }

        public interface IHighLevelCreator
        {
            IReqClient CreateClient(Uri uri);
            IReqServer CreateServer(Uri uri);
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
            public Func<Uri, IReqClient> ClientFactory;
            public Func<Uri, IReqServer> ServerFactory;

            public HighLevelCreator(string scheme, Func<Uri, IReqClient> clientFactory, Func<Uri, IReqServer> serverFactory)
            {
                ClientFactory = clientFactory;
                ServerFactory = serverFactory;
                HighLevelCreators[scheme] = this;
            }

            public IReqClient CreateClient(Uri uri)
            {
                return ClientFactory(uri);
            }
            public IReqServer CreateServer(Uri uri)
            {
                return ServerFactory(uri);
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

        public struct ExtendedConfig
        {
            public SerializationConfig SConfig;
            public IList<IClientAttachmentCreator> ClientAttachmentCreators;
            public IList<IServerAttachmentCreator> ServerAttachmentCreators;
        }

        public static IReqClient GetClient(string url, ExtendedConfig econfig)
        {
            var uri = new Uri(url);
            var scheme = uri.Scheme;
            var sconfig = econfig.SConfig ?? DefaultSerializationConfig;
            var acclient = econfig.ClientAttachmentCreators;
            //var acserver = econfig.ServerAttachmentCreators;
            var exconfig = UriUtilities.ParseExtraConfigFromQuery(uri);

            IReqClient client = null;

            IHighLevelCreator hcreator;
            if (HighLevelCreators.TryGetValue(scheme, out hcreator))
            {
                //var exconfig = ParseExtraConfigFromQuery(uri); // high-level creators should parse exconfig themselves.
                client = hcreator.CreateClient(uri);
            }
            if (client == null)
            {
                IPersistentConnectionCreator creator;
                if (Creators.TryGetValue(scheme, out creator))
                {
                    var connection = creator.CreateClient(uri);
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
                
                if (!exconfig.GetBoolean("delaystart"))
                {
                    client.Start();
                }
            }

            return client;
        }
        public static IReqClient GetClient(string url, SerializationConfig sconfig)
        {
            return GetClient(url, new ExtendedConfig() { SConfig = sconfig });
        }
        public static IReqClient GetClient(string url)
        {
            return GetClient(url, null);
        }
        public static T GetClient<T>(string url, ExtendedConfig econfig) where T : class, IReqClient
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
        public static IReqServer GetServer(string url, ExtendedConfig econfig)
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
                //var exconfig = ParseExtraConfigFromQuery(uri); // high-level creators should parse exconfig themselves.
                server = hcreator.CreateServer(uri);
            }
            if (server == null)
            {
                IPersistentConnectionCreator creator;
                if (Creators.TryGetValue(scheme, out creator))
                {
                    var connection = creator.CreateServer(uri);
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
                
                if (!exconfig.GetBoolean("delaystart"))
                {
                    server.Start();
                }
            }
            return server;
        }
        public static IReqServer GetServer(string url, SerializationConfig sconfig)
        {
            return GetServer(url, new ExtendedConfig() { SConfig = sconfig });
        }
        public static IReqServer GetServer(string url)
        {
            return GetServer(url, null);
        }
        public static T GetServer<T>(string url, ExtendedConfig econfig) where T : class, IReqServer
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
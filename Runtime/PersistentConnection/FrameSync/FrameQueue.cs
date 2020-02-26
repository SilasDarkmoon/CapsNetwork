using System;
using System.Collections.Generic;
using Capstones.UnityEngineEx;
#if (UNITY_ENGINE || UNITY_5_3_OR_NEWER) && !NET_4_6 && !NET_STANDARD_2_0
using Unity.Collections.Concurrent;
#else
using System.Collections.Concurrent;
#endif

namespace Capstones.Net.FrameSync
{
    public struct Frame
    {
        public int Index;
        public int Time;
        public int Interval;
        public ValueList<object> Ops;
    }

    /// <summary>
    /// The delegate is provided by client to handle each operate recorded by the Frame.
    /// </summary>
    /// <param name="op">The operate, is a Protocols.XXX object</param>
    /// <param name="frameTime">The time recored in Frame</param>
    /// <param name="curTime">The logic time we call this delegate. This time is >= frameTime.
    /// Handler should consider the offset(curTime - frameTime), which means the frame is a bit below the client's logic timeline.</param>
    public delegate void FrameOpHandler(object op, float frameTime, float curTime);
    public delegate bool TypedFrameOpHandler<T>(T op, float frameTime, float curTime);
    
    public sealed class FrameQueue : ConcurrentQueueGrowOnly<Frame>, IDisposable
    {
        private const int _DefaultInterval = 100;
        private Frame _LastFrame;
        private Frame _PendingFrame;
        private bool _Ended = true;

        public bool Done { get { return _Ended && _SyncTimeSinceBegin < 0 && Count == 0; } }

        public enum SyncState
        {
            Normal,
            Begin,
            BufferRunOut,
            BufferOverflow,
        }
        private SyncState _SyncState;

        // TODO: the timescale is calculated by FrameQueue's Count. If we need support variant interval between frames, we should calculate timescale by Frame's Time.
        public const int DefaultBufferBeginLength = 3;
        public const int DefaultBufferOverflowLength = 10;
        public const int DefaultBufferRefillLength = 6;
        private int _BufferBeginLength = DefaultBufferBeginLength;
        private int _BufferOverflowLength = DefaultBufferOverflowLength;
        private int _BufferRefillLength = DefaultBufferRefillLength;
        
        private void Reset()
        {
            while (TryDequeue(out _LastFrame)) ;
            _PendingFrame = _LastFrame = new Frame() { Interval = _DefaultInterval };
            _Ended = true;
            _SyncState = SyncState.Begin;
            _BufferBeginLength = DefaultBufferBeginLength;
            _BufferOverflowLength = DefaultBufferOverflowLength;
            _BufferRefillLength = DefaultBufferRefillLength;
        }
        public void Dispose()
        {
            Reset();
            TimeScale = -1.0f;
        }

        private IReqServer _ReqListener;
        public void Detach()
        {
            if (_ReqListener != null)
            {
                _ReqListener.RemoveHandler(OnFilterMessage);
            }
        }
        public void Attach(IReqServer listener)
        {
            Detach();
            _ReqListener = listener;
            if (listener != null)
            {
                listener.RegHandler(OnFilterMessage);
            }
        }

        public bool InputMessage(object raw)
        {
            if (OperationType.IsFrameSyncBegin(raw))
            {
                Reset();
                _Ended = false;
                var interval = OperationType.GetFrameSyncBeginInterval(raw);
                if (interval > 0)
                {
                    _PendingFrame.Interval = _LastFrame.Interval = interval;
                }
                _PendingFrame.Index = _LastFrame.Index = OperationType.GetFrameSyncBeginIndex(raw);
                _PendingFrame.Time = _LastFrame.Time = OperationType.GetFrameSyncBeginTime(raw);

                _LastSyncFrame = _PendingFrame;
                return false;
            }
            else if (OperationType.IsFrameSyncEnd(raw))
            {
                _Ended = true;
                return false;
            }
            else if (OperationType.IsFrameSyncTick(raw))
            {
                _LastFrame = _PendingFrame;
                var time = OperationType.GetFrameSyncTickTime(raw);
                if (time > _LastFrame.Time)
                {
                    _LastFrame.Time = time;
                }
                else
                {
                    _LastFrame.Time += _LastFrame.Interval;
                }
                Enqueue(_LastFrame);
                _PendingFrame = new Frame() { Interval = _LastFrame.Interval, Time = _LastFrame.Time, Index = _LastFrame.Index + 1 };
                var interval = OperationType.GetFrameSyncTickInterval(raw);
                if (interval > 0)
                {
                    _PendingFrame.Interval = interval;
                }
                return false;
            }
            else if (OperationType.IsFrameSyncProtocol(raw))
            {
                _PendingFrame.Ops.Add(raw);
                return false;
            }
            return true;
        }

        [EventOrder(10)]
        private object OnFilterMessage(IReqClient from, object raw, uint seq)
        {
            return InputMessage(raw) ? null : new PredefinedMessages.Raw();
        }

#region In Main Thread
        public static Action<float> DefaultOnTimeScaleChanged;
        public Action<float> OnTimeScaleChanged;
        private float _TimeScale = -1.0f;
        private float TimeScale
        {
            get { return _TimeScale < 0 ? 1.0f : _TimeScale; }
            set
            {
                if (_TimeScale != value)
                {
                    _TimeScale = value;
                    var cb = OnTimeScaleChanged ?? DefaultOnTimeScaleChanged;
                    if (cb != null)
                    {
                        cb(value);
                    }
                }
            }
        }

        public SyncState CheckSyncState()
        {
            if (_SyncState == SyncState.Normal)
            {
                if (Count > _BufferOverflowLength)
                {
                    _SyncState = SyncState.BufferOverflow;
                }
                else if (Count == 0)
                {
                    _SyncState = SyncState.BufferRunOut;
                }
            }
            else if (_SyncState == SyncState.Begin)
            {
                if (Count >= _BufferBeginLength)
                {
                    _SyncState = SyncState.Normal;
                }
            }
            else if (_SyncState == SyncState.BufferRunOut)
            {
                if (Count >= _BufferRefillLength)
                {
                    //// after we refill the frame buffer, we expand the frame buffer size.
                    //++_BufferRefillLength;
                    //++_BufferOverflowLength;
                    //++_BufferBeginLength;
                    //// ^
                    _SyncState = SyncState.Normal;
                }
            }
            else if (_SyncState == SyncState.BufferOverflow)
            {
                if (Count <= _BufferBeginLength)
                {
                    _SyncState = SyncState.Normal;
                }
            }
            return _SyncState;
        }

        public const float OverflowCatchupTime = 1.0f;
        public const float MaxTimeScale = 2.0f;
        public float CheckTimeScale()
        {
            if (_SyncState == SyncState.BufferOverflow && Count > _BufferBeginLength)
            {
                var catchupfulltime = OverflowCatchupTime;
                var catchuptime = catchupfulltime + (Count - _BufferBeginLength) * _LastFrame.Interval * 0.001f;
                var ts = catchuptime / catchupfulltime;
                if (ts > MaxTimeScale)
                {
#if DEBUG_FRAME_QUEUE
                    PlatExt.PlatDependant.LogError("Unable to determine overflow timescale - timescale too large.");
#endif
                    ts = MaxTimeScale;
                }
                return ts;
            }
            return 1.0f;
        }

        private int _LastUpdateTick;
        private float _SyncTimeSinceBegin = -1.0f;
        private float _FullTimeSinceBegin = 0f;
        private Frame _LastSyncFrame;
        public float SyncTimeSinceBegin { get { return _SyncTimeSinceBegin; } }
        public float FullTimeSinceBegin { get { return _FullTimeSinceBegin; } }
        public void Update()
        {
            var laststate = _SyncState;
            var curtick = Environment.TickCount;
            if (_SyncTimeSinceBegin < 0)
            {
                if (!_Ended)
                {
                    _LastUpdateTick = curtick;
                    _FullTimeSinceBegin = _SyncTimeSinceBegin = _LastSyncFrame.Time * 0.001f;
                }
            }
            if (_SyncTimeSinceBegin >= 0)
            {
                var curstate = CheckSyncState();
                if (laststate == SyncState.Begin)
                {
                    if (curstate == SyncState.Begin)
                    {
                        _LastUpdateTick = curtick;
                        return;
                    }
                }
                if (laststate == SyncState.BufferRunOut)
                {
                    if (_Ended)
                    {
                        TimeScale = -1.0f;
                    }
                    else if (curstate != SyncState.BufferRunOut)
                    {
                        TimeScale = -1.0f;
#if DEBUG_FRAME_QUEUE
                        PlatExt.PlatDependant.LogError("SyncFrame Run Out Restored");
#endif
                    }
                    else
                    {
                        _LastUpdateTick = curtick;
                        return;
                    }
                }
                if (!_Ended && curstate == SyncState.BufferRunOut)
                {
#if DEBUG_FRAME_QUEUE
                    PlatExt.PlatDependant.LogError("SyncFrame Run Out...");
#endif
                    TimeScale = 0f;
                    _LastUpdateTick = curtick;
                    return;
                }

                var lasttime = _SyncTimeSinceBegin;
                var curtime = _FullTimeSinceBegin = _FullTimeSinceBegin + (curtick - _LastUpdateTick) * 0.001f * TimeScale;
                _LastUpdateTick = curtick;
                var interval = ((float)_LastSyncFrame.Interval) * 0.001f;
                while (lasttime + interval <= curtime)
                {
                    Frame frame;
                    if (!TryDequeue(out frame))
                    {
                        if (_Ended)
                        {
#if DEBUG_FRAME_QUEUE
                            PlatExt.PlatDependant.LogError("SyncFrame Done");
#endif
                            TimeScale = -1.0f;
                            _SyncTimeSinceBegin = -1.0f;
                            break;
                        }
                        else
                        {
#if DEBUG_FRAME_QUEUE
                            PlatExt.PlatDependant.LogError("SyncFrame Run Out - May be caused by large timescale");
#endif
                            TimeScale = 0f;
                            break;
                        }
                    }

                    _LastSyncFrame = frame;
                    _SyncTimeSinceBegin = lasttime = frame.Time * 0.001f;
                    interval = ((float)frame.Interval) * 0.001f;
                    HandleSyncFrame();

                    curstate = CheckSyncState();
                    if (laststate == SyncState.BufferOverflow)
                    {
                        if (curstate != SyncState.BufferOverflow)
                        {
#if DEBUG_FRAME_QUEUE
                            PlatExt.PlatDependant.LogError("SyncFrame Overflow Restored");
#endif
                            TimeScale = -1.0f;
                            //// Overflow Restored - we should break now in order to not fall into RunOut
                            //// Now the TimeScale won't be too large, so we commented this out.
                            //// Another reason for commenting this out is, if we set _FullTimeSinceBegin, it will cause the renderer's time be ahead of logic time.
                            //_FullTimeSinceBegin = lasttime;
                            //break;
                        }
                        //else if (_Ended) // it seems this 'if' is useless.
                        //{

                        //}
                        else
                        {
                            var ts = CheckTimeScale();
                            if (ts > TimeScale)
                            {
                                TimeScale = ts;
                            }
                        }
                    }
                    else if (curstate == SyncState.BufferOverflow)
                    {
                        var ts = CheckTimeScale();
#if DEBUG_FRAME_QUEUE
                        PlatExt.PlatDependant.LogError("SyncFrame Overflow... time scale changed from " + TimeScale + " to " + ts);
#endif

                        TimeScale = ts;
                    }

                    laststate = curstate;
                }
            }
        }
        private void HandleSyncFrame()
        {
            for (int i = 0; i < _LastSyncFrame.Ops.Count; ++i)
            {
                var op = _LastSyncFrame.Ops[i];
                var type = op.GetType();
                FrameOpHandlerWrapper typedhandler;
                if (_TypedOpHandlers.TryGetValue(type, out typedhandler))
                {
                    if (typedhandler.CallHandler(op, _SyncTimeSinceBegin, _FullTimeSinceBegin))
                    {
                        return;
                    }
                }
                if (_GlobalTypedOpHandlers.TryGetValue(type, out typedhandler))
                {
                    if (typedhandler.CallHandler(op, _SyncTimeSinceBegin, _FullTimeSinceBegin))
                    {
                        return;
                    }
                }
                if (_CommonOpHandler != null)
                {
                    _CommonOpHandler(op, _SyncTimeSinceBegin, _FullTimeSinceBegin);
                }
                if (_GlobalCommonOpHandler != null)
                {
                    _GlobalCommonOpHandler(op, _SyncTimeSinceBegin, _FullTimeSinceBegin);
                }
            }
        }

        private class FrameOpHandlerWrapper
        {
            public FrameOpHandler CommonHandler;
            public virtual bool CallHandler(object op, float frameTime, float curTime)
            {
                if (CommonHandler != null)
                {
                    CommonHandler(op, frameTime, curTime);
                    return true;
                }
                return false;
            }
        }
        private class TypedFrameOpHandlerWrapper<T> : FrameOpHandlerWrapper
        {
            public TypedFrameOpHandler<T> TypedHandler;
            public override bool CallHandler(object op, float frameTime, float curTime)
            {
                if (TypedHandler != null)
                {
                    return TypedHandler((T)op, frameTime, curTime);
                }
                return base.CallHandler(op, frameTime, curTime);
            }
        }
        private static FrameOpHandler _GlobalCommonOpHandler;
        private static readonly Dictionary<Type, FrameOpHandlerWrapper> _GlobalTypedOpHandlers = new Dictionary<Type, FrameOpHandlerWrapper>();
        public static void SetFrameOpHandlerGlobal(Type type, FrameOpHandler handler)
        {
            if (type == null)
            {
                _GlobalCommonOpHandler = handler;
            }
            else
            {
                if (handler == null)
                {
                    _GlobalTypedOpHandlers.Remove(type);
                }
                else
                {
                    _GlobalTypedOpHandlers[type] = new FrameOpHandlerWrapper() { CommonHandler = handler };
                }
            }
        }
        public static void SetFrameOpHandlerGlobal(FrameOpHandler handler)
        {
            SetFrameOpHandlerGlobal(null, handler);
        }
        public static void SetFrameOpHandlerGlobal<T>(TypedFrameOpHandler<T> handler)
        {
            if (handler == null)
            {
                _GlobalTypedOpHandlers.Remove(typeof(T));
            }
            else
            {
                _GlobalTypedOpHandlers[typeof(T)] = new TypedFrameOpHandlerWrapper<T>() { TypedHandler = handler };
            }
        }
        private FrameOpHandler _CommonOpHandler;
        private readonly Dictionary<Type, FrameOpHandlerWrapper> _TypedOpHandlers = new Dictionary<Type, FrameOpHandlerWrapper>();
        public void SetFrameOpHandler(Type type, FrameOpHandler handler)
        {
            if (type == null)
            {
                _CommonOpHandler = handler;
            }
            else
            {
                if (handler == null)
                {
                    _TypedOpHandlers.Remove(type);
                }
                else
                {
                    _TypedOpHandlers[type] = new FrameOpHandlerWrapper() { CommonHandler = handler };
                }
            }
        }
        public void SetFrameOpHandler(FrameOpHandler handler)
        {
            SetFrameOpHandler(null, handler);
        }
        public void SetFrameOpHandler<T>(TypedFrameOpHandler<T> handler)
        {
            if (handler == null)
            {
                _TypedOpHandlers.Remove(typeof(T));
            }
            else
            {
                _TypedOpHandlers[typeof(T)] = new TypedFrameOpHandlerWrapper<T>() { TypedHandler = handler };
            }
        }
#endregion
    }
}
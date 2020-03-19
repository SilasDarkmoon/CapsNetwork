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
    public static class AttachmentExtensions
    {
#if !UNITY_ENGINE && !UNITY_5_3_OR_NEWER || NET_4_6 || NET_STANDARD_2_0
        private static ConcurrentDictionary<object, ConcurrentDictionary<string, object>> _AttachmentList = new ConcurrentDictionary<object, ConcurrentDictionary<string, object>>();
        public static void SetAttachment(object owner, string name, object attach)
        {
            if (owner == null)
            {
                return;
            }

            IChannel connection = owner as IChannel;
            if (connection == null || connection.IsAlive)
            {
                ConcurrentDictionary<string, object> attachments;
                if (connection == null)
                {
                    attachments = _AttachmentList.GetOrAdd(owner, o => new ConcurrentDictionary<string, object>());
                }
                else
                {
                    attachments = _AttachmentList.GetOrAdd(owner, o =>
                    {
                        var list = new ConcurrentDictionary<string, object>();
                        Action onClose = null;
                        onClose = () =>
                        {
                            connection.OnClose -= onClose;
                            ClearAttachments(o);
                        };
                        connection.OnClose += onClose;
                        return list;
                    });
                }
                if (attach == null)
                {
                    object old;
                    if (attachments.TryRemove(name, out old))
                    {
                        TryDispose(old);
                    }
                }
                else
                {
                    attachments.AddOrUpdate(name, attach, (n, old) =>
                    {
                        if (old != attach)
                        {
                            TryDispose(old);
                        }
                        return attach;
                    });
                }
            }
        }
        public static void ClearAttachments(object owner)
        {
            ConcurrentDictionary<string, object> attachments;
            if (_AttachmentList.TryRemove(owner, out attachments))
            {
                var arr = _AttachmentList.ToArray();
                for (int i = 0; i < arr.Length; ++i)
                {
                    var attach = arr[i].Value;
                    TryDispose(attach);
                }
            }
        }
        public static object GetAttachment(object owner, string name)
        {
            if (owner == null)
            {
                return null;
            }
            ConcurrentDictionary<string, object> attachments;
            if (_AttachmentList.TryGetValue(owner, out attachments))
            {
                object attach;
                attachments.TryGetValue(name, out attach);
                return attach;
            }
            return null;
        }
#else
        private static Dictionary<object, Dictionary<string, object>> _AttachmentList = new Dictionary<object, Dictionary<string, object>>();
        public static void SetAttachment(object owner, string name, object attach)
        {
            if (owner == null)
            {
                return;
            }

            IConnectionStatus connection = owner as IConnectionStatus;
            if (connection == null || connection.IsAlive)
            {
                Dictionary<string, object> attachments;
                bool createdlist = false;
                lock (_AttachmentList)
                {
                    if (!_AttachmentList.TryGetValue(owner, out attachments))
                    {
                        createdlist = true;
                        _AttachmentList[owner] = attachments = new Dictionary<string, object>();
                    }
                }
                if (createdlist && connection != null)
                {
                    Action onClose = null;
                    onClose = () =>
                    {
                        connection.OnClose -= onClose;
                        ClearAttachments(owner);
                    };
                    connection.OnClose += onClose;
                }
                object old;
                lock (attachments)
                {
                    attachments.TryGetValue(name, out old);
                    if (attach == null)
                    {
                        attachments.Remove(name);
                    }
                    else
                    {
                        attachments[name] = attach;
                    }
                }
                if (old != null && old != attach)
                {
                    TryDispose(old);
                }
            }
        }
        public static void ClearAttachments(object owner)
        {
            Dictionary<string, object> attachments;
            lock (_AttachmentList)
            {
                if (_AttachmentList.TryGetValue(owner, out attachments))
                {
                    _AttachmentList.Remove(owner);
                }
            }
            if (attachments != null)
            {
                List<object> attachlist = new List<object>();
                lock (attachments)
                {
                    foreach (var kvp in attachments)
                    {
                        attachlist.Add(kvp.Value);
                    }
                    attachments.Clear();
                }
                for (int i = 0; i < attachlist.Count; ++i)
                {
                    TryDispose(attachlist[i]);
                }
            }
        }
        public static object GetAttachment(object owner, string name)
        {
            if (owner == null)
            {
                return null;
            }
            Dictionary<string, object> attachments;
            lock (_AttachmentList)
            {
                _AttachmentList.TryGetValue(owner, out attachments);
            }
            if (attachments != null)
            { 
                lock (attachments)
                {
                    object attach;
                    attachments.TryGetValue(name, out attach);
                    return attach;
                }
            }
            return null;
        }
#endif
        public static void SetAttachment(this IChannel owner, string name, object attach)
        {
            SetAttachment((object)owner, name, attach);
        }
        public static void SetAttachment(this IChannel owner, object attach)
        {
            if (attach != null)
            {
                SetAttachment(owner, attach.GetType().Name, attach);
            }
        }
        public static object GetAttachment(this IChannel owner, string name)
        {
            return GetAttachment((object)owner, name);
        }
        public static T GetAttachment<T>(this IChannel owner, string name)
        {
            return (T)GetAttachment((object)owner, name);
        }
        public static T GetAttachment<T>(this IChannel owner)
        {
            return (T)GetAttachment((object)owner, typeof(T).Name);
        }
        public static void TryDispose(object obj)
        {
            var disposable = obj as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        public interface IAttachmentCreator
        {
            string Name { get; }
            object CreateAttachment(object owner);
        }
        public interface IAttachmentCreator<T> : IAttachmentCreator
        {
            object CreateAttachment(T owner);
        }
    }

    #region Attachments
    public class RTTMeasure
    {
        public const int TrackedRTTCnt = 10;
        protected int[] _TrackedRTT = new int[TrackedRTTCnt];
        protected int _NextTrackedIndex = 0;
        protected int _RTT = -1;
        public int RTT { get { return _RTT; } }

        public RTTMeasure()
        {
            for (int i = 0; i < TrackedRTTCnt; ++i)
            {
                _TrackedRTT[i] = -1;
            }
            _LastTick = Environment.TickCount;
        }

        protected int CalculateRTT()
        {
            int cnt = 0;
            int total = 0;
            for (int i = 0; i < TrackedRTTCnt; ++i)
            {
                var rtt = _TrackedRTT[i];
                if (rtt >= 0)
                {
                    ++cnt;
                    total += rtt;
                }
            }
            return _RTT = total / cnt;
        }

        public void RecordRTT(int rtt)
        {
            if (rtt >= 0)
            {
                _TrackedRTT[_NextTrackedIndex++ % TrackedRTTCnt] = rtt;
                CalculateRTT();
            }
            _LastTick = Environment.TickCount;
        }

        protected int _LastTick;
        public int LastTick { get { return _LastTick; } }
        public void RecordTick()
        {
            _LastTick = Environment.TickCount;
        }
    }

    public class Heartbeat : RTTMeasure, IDisposable
    {
        protected IReqClient _Client;
        public object _HeartbeatObj;
        public Func<object> _HeartbeatCreator;

        public int Interval = 1000;
        public int Timeout = -1;
        protected bool _Dead = false;
        public bool Dead { get { return _Dead; } }

        public event Action OnDead = () => { };
        protected void OnHeartbeatDead()
        {
            PlatDependant.LogError("Cannot receive heartbeat response for a long time. Closing...");
            _Dead = true;
            var disposable = _Client as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
            OnDead();
        }

        public Heartbeat(IReqClient client)
        {
            _Client = client;
            Start();
        }
        public Heartbeat(IReqClient client, object heartbeatObj)
            : this(client)
        {
            _HeartbeatObj = heartbeatObj;
        }
        public Heartbeat(IReqClient client, Func<object> heartbeatCreator)
            : this(client)
        {
            _HeartbeatCreator = heartbeatCreator;
        }

        public void Start()
        {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
            if (ThreadSafeValues.IsMainThread)
            {
                CoroutineRunner.StartCoroutine(SendHeartbeatWork());
            }
            else
#endif
            {
                PlatDependant.RunBackground(prog =>
                {
                    try
                    {
                        _LastTick = Environment.TickCount;
                        while (_Client.IsAlive && !_Disposed)
                        {
                            if (_Client.IsStarted)
                            {
                                object heartbeat = null;
                                if (_HeartbeatCreator != null)
                                {
                                    heartbeat = _HeartbeatCreator();
                                }
                                if (heartbeat == null)
                                {
                                    heartbeat = _HeartbeatObj;
                                }
                                if (heartbeat == null)
                                {
                                    heartbeat = PredefinedMessages.Empty;
                                }
                                SendHeartbeatAsync(heartbeat);
                            }
                            else
                            {
                                _LastTick = Environment.TickCount;
                            }
                            var interval = Interval;
                            if (interval < 0)
                            {
                                interval = 1000;
                            }
                            Thread.Sleep(interval);
                            if (Timeout > 0 && Environment.TickCount > _LastTick + Timeout)
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        if (_Client.IsAlive && !_Disposed)
                        {
                            OnHeartbeatDead();
                        }
                    }
                });
            }
        }

#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
        protected IEnumerator SendHeartbeatWork()
        {
            try
            {
                _LastTick = Environment.TickCount;
                while (_Client.IsAlive && !_Disposed)
                {
                    if (_Client.IsStarted)
                    {
                        object heartbeat = null;
                        if (_HeartbeatCreator != null)
                        {
                            heartbeat = _HeartbeatCreator();
                        }
                        if (heartbeat == null)
                        {
                            heartbeat = _HeartbeatObj;
                        }
                        if (heartbeat == null)
                        {
                            heartbeat = PredefinedMessages.Empty;
                        }
                        SendHeartbeatAsync(heartbeat);
                    }
                    else
                    {
                        _LastTick = Environment.TickCount;
                    }
                    var interval = Interval;
                    if (interval < 0)
                    {
                        interval = 1000;
                    }
                    yield return new WaitForSecondsRealtime(interval / 1000f);
                    if (Timeout > 0 && Environment.TickCount - _LastTick > Timeout)
                    {
                        Debug.LogError($"Heartbeat Timedout. Current{Environment.TickCount}, Last{_LastTick}.");
                        break;
                    }
                }
            }
            finally
            {
                if (_Client.IsAlive && !_Disposed)
                {
                    OnHeartbeatDead();
                }
            }
        }
#endif

        protected async void SendHeartbeatAsync(object heartbeat)
        {
            var request = _Client.Send(heartbeat, 1000);
            await request;
            if (request.Error == null)
            {
                RecordRTT(request.RTT);
            }
            else
            {
                PlatDependant.LogError(request.Error);
            }
        }

        protected bool _Disposed;
        public void Dispose()
        {
            if (!_Disposed)
            {
                _Disposed = true;
            }
        }
    }

    public class ReqQueue : ConcurrentQueueGrowOnly<object>, IDisposable
    {
        protected IReqServer _Server;
        public ReqQueue(IReqServer server)
        {
            _Server = server;
            server.RegHandler(HandleMessage);
        }

        [EventOrder(80)]
        protected object HandleMessage(IReqClient from, uint type, object req, uint seq)
        {
            if (req != null)
            {
                Enqueue(req);
            }
            return null;
        }

        protected bool _Disposed;
        public void Dispose()
        {
            if (!_Disposed)
            {
                _Disposed = true;
                _Server.RemoveHandler(HandleMessage);
            }
        }
    }
    #endregion
}
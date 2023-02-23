using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public delegate void CommonHandler(IPersistentConnection thiz);
    public delegate void ReceiveHandler(byte[] buffer, int cnt, EndPoint sender);
    //public delegate void SendCompleteHandler(bool success);
    public delegate bool SendHandler(IPooledBuffer buffer, int cnt);
    public delegate ValueList<PooledBufferSpan> SendSerializer(object obj);
    public delegate int UpdateHandler(IPersistentConnection thiz);
    
    public interface IPersistentConnectionLifetime : IDisposable
    {
        void Start();
        bool IsStarted { get; }
        bool IsAlive { get; }
    }
    public interface IServerConnectionLifetime
    {
        bool IsConnected { get; }
        event Action<IServerConnectionLifetime> OnConnected;
    }
    public interface IPersistentConnection : IPersistentConnectionLifetime
    {
        EndPoint RemoteEndPoint { get; }
        ReceiveHandler OnReceive { get; set; }
        UpdateHandler OnUpdate { get; set; }
        CommonHandler OnClose { get; set; }
        void Send(IPooledBuffer data, int cnt);
        void Send(ValueList<PooledBufferSpan> data); // the buffer in data do not need to AddRef and can be released directly.
        void Send(object data, SendSerializer serializer);
        //SendCompleteHandler OnSendComplete { get; set; }
    }
    public interface IServerConnection : IPersistentConnection, IServerConnectionLifetime
    {
    }
    public interface IAutoPackedConnection
    {
    }

    public interface ICustomSendConnection : IPersistentConnection
    {
        SendHandler OnSend { get; set; }
        void SendRaw(byte[] data, int cnt, Action<bool> onComplete);
    }

    public interface IPositiveConnection
    {
        bool PositiveMode { get; set; }
        void Step();
    }

    public interface IPersistentConnectionServer : IPersistentConnectionLifetime, IServerConnectionLifetime
    {
        IServerConnection PrepareConnection();
    }
}

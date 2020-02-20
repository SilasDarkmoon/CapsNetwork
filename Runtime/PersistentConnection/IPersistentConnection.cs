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

    public interface IPersistentConnection
    {
        void StartConnect();
        bool IsConnectionAlive { get; }
        EndPoint RemoteEndPoint { get; }
        ReceiveHandler OnReceive { get; set; }
        UpdateHandler OnUpdate { get; set; }
        void Send(IPooledBuffer data, int cnt);
        void Send(ValueList<PooledBufferSpan> data); // the buffer in data do not need to AddRef and can be released directly.
        void Send(object data, SendSerializer serializer);
        //SendCompleteHandler OnSendComplete { get; set; }
    }
    public interface IServerConnection : IPersistentConnection
    {
        bool IsConnected { get; }
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

    public interface IPersistentConnectionServer
    {
        void StartListening();
        bool IsAlive { get; }
        IServerConnection PrepareConnection();
    }
}

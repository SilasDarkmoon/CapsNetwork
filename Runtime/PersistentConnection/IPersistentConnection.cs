using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Unity.Collections.Concurrent;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public delegate void CommonHandler(IPersistentConnection thiz);
    public delegate void ReceiveHandler(byte[] buffer, int cnt, EndPoint sender);
    public delegate void SendCompleteHandler(byte[] buffer, bool success);
    public delegate bool SendHandler(byte[] buffer, int cnt);

    public interface IPersistentConnection
    {
        void StartConnect();
        bool IsConnectionAlive { get; }
        EndPoint RemoteEndPoint { get; }
        ReceiveHandler OnReceive { get; set; }
        void Send(byte[] data, int cnt);
        SendCompleteHandler OnSendComplete { get; set; }
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

    public interface IPersistentConnectionServer
    {
        void StartListening();
        bool IsAlive { get; }
        IServerConnection PrepareConnection();
    }
}

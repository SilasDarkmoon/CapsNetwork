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
    public class KCPConsultServer : KCPServer, IPersistentConnectionServer
    {
        public new class ServerConnection : KCPServer.ServerConnection
        {
            protected internal uint _PendingConv;
            protected internal Guid _PendingGUID = Guid.Empty;

            protected internal ServerConnection(KCPConsultServer server, uint pendingconv) : base(server)
            {
                _PendingConv = pendingconv;
                _Conv = 0;
                _KCP = KCPLib.CreateConnection(1, (IntPtr)_InfoHandle);
                _Ready = true;

                _KCP.SetOutput(Func_KCPOutput);
                _KCP.NoDelay(1, 10, 2, 1);
                // set minrto to 10?
            }

            protected internal override bool Feed(byte[] data, int cnt, IPEndPoint ep)
            {
                if (_Conv == 0)
                { // this means the conv has not been accepted by client.
                    var conv = ReadConv(data, cnt);
                    if (conv == 0)
                    { // wrong packet.
                        return false;
                    }
                    else if (conv == 1)
                    { // the unaccepted connection
                        var guid = ReadGUID(data, cnt);
                        if (guid == Guid.Empty)
                        {
                            if (EP != null && EP.Equals(ep))
                            { // this means the ack-packet or something else.
                                if (_KCP.Input(data, cnt) == 0)
                                {
                                    ReceiveFromKCP();
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            }
                            else
                            { // client should provide a guid for new connection
                                return false;
                            }
                        }
                        else
                        {
                            if (_PendingGUID == Guid.Empty)
                            { // accept this connection. bind this connection with the guid.
                                if (_KCP.Input(data, cnt) == 0)
                                {
                                    _PendingGUID = guid;
                                    EP = ep;
                                    // send the pending conv-id to client.
                                    var pinfo = BufferPool.GetBufferFromPool();
                                    byte[] buffer = pinfo.Buffer;
                                    if (BitConverter.IsLittleEndian)
                                    {
                                        var pconv = _PendingConv;
                                        for (int i = 0; i < 4; ++i)
                                        {
                                            buffer[i] = (byte)((pconv >> (i * 8)) & 0xFF);
                                        }
                                    }
                                    else
                                    {
                                        var pconv = _PendingConv;
                                        for (int i = 0; i < 4; ++i)
                                        {
                                            buffer[i] = (byte)((pconv >> ((3 - i) * 8)) & 0xFF);
                                        }
                                    }
                                    _KCP.Send(buffer, 4);
                                    pinfo.Release();
                                    _Connected = true;
                                    FireOnConnected();
                                    ReceiveFromKCP();
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            }
                            else
                            { // check the guid.
                                if (_PendingGUID == guid)
                                {
                                    if (_KCP.Input(data, cnt) == 0)
                                    {
                                        if (!ep.Equals(EP))
                                        { // check the ep changed?
                                            EP = ep;
                                        }
                                        ReceiveFromKCP();
                                        return true;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                                else
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    else
                    { // the first packet from accepted connection?
                        if (conv == _PendingConv)
                        { // the first packet from accepted connection!
                            // change the kcp to real conv-id.
                            _Conv = conv;
                            _KCP.Release();
                            _KCP = KCPLib.CreateConnection(conv, (IntPtr)_InfoHandle);

                            _KCP.SetOutput(Func_KCPOutput);
                            _KCP.NoDelay(1, 10, 2, 1);
                            // set minrto to 10?

                            if (!ep.Equals(EP))
                            { // check the ep changed?
                                EP = ep;
                            }

                            // Feed the data.
                            if (_KCP.Input(data, cnt) == 0)
                            {
                                ReceiveFromKCP();
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else
                        { // this packet is for other connection.
                            return false;
                        }
                    }
                }
                else
                { // the normal connection.
                    return base.Feed(data, cnt, ep);
                }
            }
            protected internal override int Update()
            {
                if (_Conv == 0)
                {
                    _KCP.Update((uint)Environment.TickCount);
                    //_KCP.Receive(_RecvBuffer, CONST.MTU); // what is it used for?
                    ReceiveFromKCP();

                    if (_OnUpdate != null)
                    {
                        return _OnUpdate(this);
                    }
                    else
                    {
                        return int.MinValue;
                    }
                }
                else
                {
                    return base.Update();
                }
            }

            public static uint ReadConv(byte[] data, int cnt)
            {
                if (cnt < 4)
                {
                    return 0;
                }
                if (BitConverter.IsLittleEndian)
                {
                    return BitConverter.ToUInt32(data, 0);
                }
                else
                {
                    uint conv = 0;
                    for (int i = 0; i < 4; ++i)
                    {
                        conv <<= 8;
                        conv += data[i];
                    }
                    return conv;
                }
            }
            public static Guid ReadGUID(byte[] data, int cnt)
            {
                if (cnt < 40)
                {
                    return Guid.Empty;
                }
                // because we use this guid locally so we donot care the endian.
                return new Guid(BitConverter.ToInt32(data, 24), BitConverter.ToInt16(data, 28), BitConverter.ToInt16(data, 30), data[32], data[33], data[34], data[35], data[36], data[37], data[38], data[39]);
            }
        }

        public KCPConsultServer(int port) : base(port) { }

        protected int _LastConv = 1;
        public override KCPServer.ServerConnection PrepareConnection()
        {
            var con = new ServerConnection(this, (uint)Interlocked.Increment(ref _LastConv));
            con.OnConnected += OnChildConnected;
            lock (_Connections)
            {
                _Connections.Add(con);
            }
            return con;
        }
        protected new void OnChildConnected(IServerConnectionLifetime child)
        {
            child.OnConnected -= OnChildConnected;
            FireOnConnected(child);
        }
    }

    public static partial class ConnectionFactory
    {
        private static RegisteredCreator _Reg_KCP = new RegisteredCreator("kcp"
            , uri => new KCPConsultClient(uri.ToString())
            , uri =>
            {
                var port = uri.Port;
                return new KCPConsultServer(port);
            });
    }
}

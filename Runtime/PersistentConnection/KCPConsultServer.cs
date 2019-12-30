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
                _KCP = KCPLib.kcp_create(1, (IntPtr)_InfoHandle);
                _Ready = true;

                _KCP.kcp_setoutput(Func_KCPOutput);
                _KCP.kcp_nodelay(1, 10, 2, 1);
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
                                return _KCP.kcp_input(data, cnt) == 0;
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
                                if (_KCP.kcp_input(data, cnt) == 0)
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
                                    _KCP.kcp_send(buffer, 4);
                                    pinfo.Release();
                                    _Connected = true;
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
                                    if (_KCP.kcp_input(data, cnt) == 0)
                                    {
                                        if (!ep.Equals(EP))
                                        { // check the ep changed?
                                            EP = ep;
                                        }
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
                            _KCP.kcp_release();
                            _KCP = KCPLib.kcp_create(conv, (IntPtr)_InfoHandle);

                            _KCP.kcp_setoutput(Func_KCPOutput);
                            _KCP.kcp_nodelay(1, 10, 2, 1);
                            // set minrto to 10?

                            if (!ep.Equals(EP))
                            { // check the ep changed?
                                EP = ep;
                            }

                            // Feed the data.
                            return _KCP.kcp_input(data, cnt) == 0;
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
            protected internal override void Update()
            {
                if (_Conv == 0)
                {
                    _KCP.kcp_update((uint)Environment.TickCount);
                    _KCP.kcp_recv(_RecvBuffer, CONST.MTU);
                }
                else
                {
                    base.Update();
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

        protected static int _LastConv = 1;
        public override KCPServer.ServerConnection PrepareConnection()
        {
            var con = new ServerConnection(this, (uint)Interlocked.Increment(ref _LastConv));
            lock (_Connections)
            {
                _Connections.Add(con);
            }
            return con;
        }
    }

    public static partial class PersistentConnectionFactory
    {
        private static RegisteredCreator _Reg_KCP = new RegisteredCreator("kcp"
            , url => new KCPConsultClient(url)
            , url =>
            {
                var uri = new Uri(url);
                var port = uri.Port;
                return new KCPConsultServer(port);
            });
    }
}

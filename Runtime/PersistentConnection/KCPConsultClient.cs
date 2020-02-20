using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;

using PlatDependant = Capstones.UnityEngineEx.PlatDependant;
using TaskProgress = Capstones.UnityEngineEx.TaskProgress;

namespace Capstones.Net
{
    public class KCPConsultClient : KCPClient
    {
        protected Guid _ConnectionGUID = Guid.NewGuid();

        public KCPConsultClient(string url) : base(url, 1)
        {
            _Conv = 0;
            _Connection.OnUpdate = _con =>
            {
                _KCP.kcp_update((uint)Environment.TickCount);
                int recvcnt = _KCP.kcp_recv(_RecvBuffer, CONST.MTU);
                if (_Conv == 0)
                {
                    if (recvcnt >= 4)
                    {
                        uint conv = 0;
                        if (BitConverter.IsLittleEndian)
                        {
                            conv = BitConverter.ToUInt32(_RecvBuffer, 0);
                        }
                        else
                        {
                            for (int i = 0; i < 4; ++i)
                            {
                                conv <<= 8;
                                conv += _RecvBuffer[i];
                            }
                        }
                        if (conv == 0 || conv == 1)
                        {
                            PlatDependant.LogError("KCP conversation id should not be 0 or 1 (with Consult).");
                            throw new ArgumentException("KCP conversation id should not be 0 or 1 (with Consult).");
                        }
                        _KCP.kcp_release();

                        _Conv = conv;
                        _KCP = KCPLib.kcp_create(conv, (IntPtr)_ConnectionHandle);
                        _KCP.kcp_setoutput(Func_KCPOutput);
                        _KCP.kcp_nodelay(1, 10, 2, 1);
                        _Connection.HoldSending = false;
                    }
                }
                else
                {
                    if (_OnReceive != null)
                    {
                        if (recvcnt > 0)
                        {
                            _OnReceive(_RecvBuffer, recvcnt, _Connection.RemoteEndPoint);
                        }
                    }
                }
                if (_OnUpdate != null)
                {
                    return _OnUpdate(this);
                }
                else
                {
                    return int.MinValue;
                }
            };
            _Connection.PreStart = _con =>
            {
                var guid = _ConnectionGUID.ToByteArray();
                _KCP.kcp_send(guid, guid.Length);
            };
            _Connection.HoldSending = true;
        }
    }
}

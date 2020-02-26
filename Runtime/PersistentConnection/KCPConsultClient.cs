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
                _KCP.Update((uint)Environment.TickCount);
                int recvcnt = _KCP.Receive(_RecvBuffer, CONST.MTU);
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
                        _KCP.Release();

                        _Conv = conv;
                        _KCP = KCPLib.CreateConnection(conv, (IntPtr)_ConnectionHandle);
                        _KCP.SetOutput(Func_KCPOutput);
                        _KCP.NoDelay(1, 10, 2, 1);
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
                _KCP.Send(guid, guid.Length);
            };
            _Connection.HoldSending = true;
        }
    }
}

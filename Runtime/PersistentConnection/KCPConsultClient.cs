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
            _Connection.PreStart = _con =>
            {
                var guid = _ConnectionGUID.ToByteArray();
                _KCP.Send(guid, guid.Length);
            };
            _Connection.HoldSending = true;
        }

        protected override void ReceiveFromKCP()
        {
            int recvcnt;
            while ((recvcnt = _KCP.Receive(_RecvBuffer, CONST.MTU)) > 0)
            {
                OnReceiveFromKCP(_RecvBuffer, recvcnt);
            }
            if (recvcnt == -3)
            {
                PlatDependant.LogError("Receive from kcp error - buffer is too small.");
                byte[] buffer;
                for (int i = 2; ; ++i)
                {
                    buffer = new byte[CONST.MTU * 2];
                    recvcnt = _KCP.Receive(buffer, buffer.Length);
                    if (recvcnt > 0)
                    {
                        OnReceiveFromKCP(buffer, recvcnt);
                        break;
                    }
                    else if (recvcnt != 0 && recvcnt != -3)
                    {
                        PlatDependant.LogError("Receive from kcp error - code " + recvcnt);
                    }
                }
            }
        }
        protected void OnReceiveFromKCP(byte[] buffer, int cnt)
        {
            if (_Conv == 0)
            {
                if (cnt >= 4)
                {
                    uint conv = 0;
                    if (BitConverter.IsLittleEndian)
                    {
                        conv = BitConverter.ToUInt32(buffer, 0);
                    }
                    else
                    {
                        for (int i = 0; i < 4; ++i)
                        {
                            conv <<= 8;
                            conv += buffer[i];
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

                    for (int i = 4; i < cnt; ++i)
                    {
                        buffer[i - 4] = buffer[i];
                    }
                    cnt -= 4;
                }
            }
            if (cnt > 0)
            {
                if (_OnReceive != null)
                {
                    _OnReceive(buffer, cnt, _Connection.RemoteEndPoint);
                }
            }
        }
    }
}

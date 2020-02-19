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
    public class KCPClient : IPersistentConnection, IDisposable
    {
        protected uint _Conv;
        protected UDPClient _Connection;
        protected GCHandle _ConnectionHandle;
        protected KCPLib.Connection _KCP;
        private bool _Disposed = false;
        protected byte[] _RecvBuffer = new byte[CONST.MTU];
        private static char[] _PathSplitChars = new[] { '/', '\\' };

        public KCPClient(string url, uint conv)
        {
            Init(url, conv);
        }
        public KCPClient(string url)
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.Trim(_PathSplitChars);
            var index = path.IndexOfAny(_PathSplitChars);
            if (index > 0)
            {
                path = path.Substring(0, index);
            }
            uint conv;
            uint.TryParse(path, out conv);
            Init(url, conv);
        }
        private void Init(string url, uint conv)
        {
            if (conv == 0)
            {
                PlatDependant.LogError("KCP conversation id should not be 0.");
            }
            _Connection = new UDPClient(url);
            _Conv = conv;
            _ConnectionHandle = GCHandle.Alloc(_Connection);
            _KCP = KCPLib.kcp_create(conv, (IntPtr)_ConnectionHandle);

            _KCP.kcp_setoutput(Func_KCPOutput);
            _KCP.kcp_nodelay(1, 10, 2, 1);
            // set minrto to 10?

            _Connection.UpdateInterval = 10;
            _Connection.PreDispose = _con => DisposeSelf();
            _Connection.OnReceive = (data, cnt, sender) => _KCP.kcp_input(data, cnt);
            _Connection.OnSend = (data, cnt) =>
            {
                if (cnt > CONST.MTU)
                {
                    int offset = 0;
                    var info = BufferPool.GetBufferFromPool();
                    var buffer = info.Buffer;
                    while (cnt > CONST.MTU)
                    {
                        Buffer.BlockCopy(data.Buffer, offset, buffer, 0, CONST.MTU);
                        _KCP.kcp_send(buffer, CONST.MTU);
                        cnt -= CONST.MTU;
                        offset += CONST.MTU;
                    }
                    if (cnt > 0)
                    {
                        Buffer.BlockCopy(data.Buffer, offset, buffer, 0, cnt);
                        _KCP.kcp_send(buffer, cnt);
                    }
                    info.Release();
                }
                else
                {
                    _KCP.kcp_send(data.Buffer, cnt);
                }
                return true;
            };
            _Connection.OnUpdate = _con =>
            {
                _KCP.kcp_update((uint)Environment.TickCount);
                int recvcnt = _KCP.kcp_recv(_RecvBuffer, CONST.MTU);
                if (_OnReceive != null)
                {
                    if (recvcnt > 0)
                    {
                        _OnReceive(_RecvBuffer, recvcnt, _Connection.RemoteEndPoint);
                    }
                }
            };
        }

        private void DisposeSelf()
        {
            if (!_Disposed)
            {
                _Disposed = true;
                _KCP.kcp_release();
                _ConnectionHandle.Free();
                //_Connection = null; // the connection should be disposed alreay, so we donot need to set it to null.

                // set handlers to null.
                _OnReceive = null;
            }
        }

        protected static KCPLib.kcp_output Func_KCPOutput = new KCPLib.kcp_output(KCPOutput);
        [AOT.MonoPInvokeCallback(typeof(KCPLib.kcp_output))]
        private static int KCPOutput(IntPtr buf, int len, KCPLib.Connection kcp, IntPtr user)
        {
            try
            {
                var gchandle = (GCHandle)user;
                var connection = gchandle.Target as UDPClient;
                if (connection != null)
                {
                    var info = BufferPool.GetBufferFromPool(len);
                    Marshal.Copy(buf, info.Buffer, 0, len);
                    connection.SendRaw(info, len
                        //, success => BufferPool.ReturnRawBufferToPool(buffer)
                        );
                    info.Release();
                }
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
            }
            return 0;
        }

        public bool IsConnectionAlive
        {
            get { return _Connection.IsConnectionAlive; }
        }
        public EndPoint RemoteEndPoint
        {
            get { return _Connection.RemoteEndPoint; }
        }
        protected ReceiveHandler _OnReceive;
        /// <summary>
        /// This will be called in connection thread.
        /// </summary>
        public ReceiveHandler OnReceive
        {
            get { return _OnReceive; }
            set
            {
                if (value != _OnReceive)
                {
                    if (IsConnectionAlive)
                    {
                        PlatDependant.LogError("Cannot change OnReceive when connection started");
                    }
                    else
                    {
                        _OnReceive = value;
                    }
                }
            }
        }
        //public SendCompleteHandler OnSendComplete
        //{
        //    get { return _Connection.OnSendComplete; }
        //    set { _Connection.OnSendComplete = value; }
        //}
        public virtual void StartConnect()
        {
            _Connection.StartConnect();
        }
        public void Send(IPooledBuffer data, int cnt)
        {
            _Connection.Send(data, cnt);
        }
        public void Send(ValueList<PooledBufferSpan> data)
        {
            _Connection.Send(data);
        }
        public void Send(object raw, SendSerializer serializer)
        {
            _Connection.Send(raw, serializer);
        }
        public void Send(byte[] data, int cnt)
        {
            Send(new UnpooledBuffer(data), cnt);
        }
        public void Send(byte[] data)
        {
            Send(data, data.Length);
        }

        public void Dispose()
        {
            Dispose(false);
        }
        public void Dispose(bool inFinalizer)
        {
            _Connection.Dispose(inFinalizer);
        }
        ~KCPClient()
        {
            Dispose(true);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using Capstones.UnityEngineEx;
using System.IO;

namespace Capstones.Net
{
    public delegate void StreamReceiveHandler(byte[] data, int offset, int cnt);
    public interface INotifyReceiveStream
    {
        event StreamReceiveHandler OnReceive;
    }
    public class ConnectionStream : BidirectionMemStream, INotifyReceiveStream
    {
        private IPersistentConnection _Con;
        private bool _LeaveOpen;
        public ConnectionStream(IPersistentConnection con, bool leaveOpen)
        {
            if (con != null)
            {
                _Con = con;
                con.OnReceive = (data, cnt, sender) => Receive(data, 0, cnt);
                //con.OnSendComplete = (buffer, success) => { if (buffer != null) BufferPool.ReturnRawBufferToPool(buffer); };
            }
            _LeaveOpen = leaveOpen;
        }
        public ConnectionStream(IPersistentConnection con) : this(con, false) { }

        public event StreamReceiveHandler OnReceive = (data, offset, cnt) => { };
        public bool DonotNotifyReceive = false;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_Con != null)
            {
                int cntwrote = 0;
                while (cntwrote < count)
                {
                    var pbuffer = BufferPool.GetBufferFromPool();
                    var sbuffer = pbuffer.Buffer;
                    int scnt = count - cntwrote;
                    if (sbuffer.Length < scnt)
                    {
                        scnt = sbuffer.Length;
                    }
                    Buffer.BlockCopy(buffer, offset + cntwrote, sbuffer, 0, scnt);

                    _Con.Send(pbuffer, scnt);
                    pbuffer.Release();

                    cntwrote += scnt;
                }
            }
        }
        public void Write(IList<byte> buffer, int offset, int count)
        {
            if (_Con != null)
            {
                int cntwrote = 0;
                while (cntwrote < count)
                {
                    var pbuffer = BufferPool.GetBufferFromPool();
                    var sbuffer = pbuffer.Buffer;
                    int scnt = count - cntwrote;
                    if (sbuffer.Length < scnt)
                    {
                        scnt = sbuffer.Length;
                    }
                    for (int i = 0; i < scnt; ++i)
                    {
                        sbuffer[i] = buffer[offset + cntwrote + i];
                    }

                    _Con.Send(pbuffer, scnt);
                    pbuffer.Release();

                    cntwrote += scnt;
                }
            }
        }
        public void Write(NativeBufferStream buffer, int offset, int count)
        {
            if (_Con != null)
            {
                ValueList<PooledBufferSpan> buffers = new ValueList<PooledBufferSpan>();
                buffer.Seek(0, SeekOrigin.Begin);
                int cntwrote = 0;
                while (cntwrote < count)
                {
                    var pbuffer = BufferPool.GetBufferFromPool();
                    var sbuffer = pbuffer.Buffer;
                    int scnt = count - cntwrote;
                    if (sbuffer.Length < scnt)
                    {
                        scnt = sbuffer.Length;
                    }
                    buffer.Read(sbuffer, 0, scnt);
                    buffers.Add(new PooledBufferSpan() { WholeBuffer = pbuffer, Length = scnt });
                    cntwrote += scnt;
                }
                _Con.Send(buffers);
            }
        }
        public void Write(object raw, SendSerializer serializer)
        {
            if (_Con != null)
            {
                _Con.Send(raw, serializer);
            }
        }
        private void Receive(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            if (!DonotNotifyReceive)
            {
                OnReceive(buffer, offset, count);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!_LeaveOpen)
            {
                var disp = _Con as IDisposable;
                if (disp != null)
                {
                    disp.Dispose();
                }
            }
            _Con = null;
        }
    }
}

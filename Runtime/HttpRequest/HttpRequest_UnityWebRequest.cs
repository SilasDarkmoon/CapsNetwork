using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public class HttpRequest : HttpRequestBase
    {
        public HttpRequest(string url, HttpRequestData headers, HttpRequestData data, string dest)
            : base(url, headers, data, dest)
        {
        }
        public HttpRequest(string url, HttpRequestData data, string dest)
            : this(url, null, data, dest)
        {
        }
        public HttpRequest(string url, string dest)
            : this(url, null, null, dest)
        {
        }
        public HttpRequest(string url, HttpRequestData data)
            : this(url, null, data, null)
        {
        }
        public HttpRequest(string url)
            : this(url, null, null, null)
        {
        }

        protected UnityWebRequest _InnerReq;
        protected byte[] _ReceiveBuffer = new byte[64 * 1024];
        protected BidirectionMemStream _ReceiveStream = new BidirectionMemStream();
        protected class DownloadHandler : DownloadHandlerScript
        {
            protected HttpRequest _Req;

            public DownloadHandler(HttpRequest req)
                : base(req._ReceiveBuffer)
            {
                _Req = req;
            }

            protected override void ReceiveContentLengthHeader(ulong contentLength)
            {
                _Req._Total += contentLength;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                _Req._ReceiveStream.Write(data, 0, dataLength);
                if (_Req.ToMem)
                {
                    _Req._Length += (ulong)dataLength;
                }
                return true;
            }
            //protected override void CompleteContent()
            //{
            //    CoroutineRunner.StartCoroutine(_Req.WaitForDone());
            //}
        }

        protected System.IO.Stream _FinalDestStream;
        protected ulong _DestStartOffset = 0;
        //protected bool _ToMem = false;
        //public bool ToMem { get { return _ToMem; } }
        protected bool ToMem { get { return _FinalDestStream is MemoryStream; } }
        protected bool ToExternal { get { return _FinalDestStream == _DestStream; } }

        public override void StartRequest()
        {
            if (_Status == HttpRequestStatus.NotStarted)
            {
                _Status = HttpRequestStatus.Running;

                _InnerReq = new UnityWebRequest(_Url);
                if (_Timeout > 0)
                {
                    _InnerReq.timeout = _Timeout / 1000;
                }

                var data = PrepareRequestData();
                if (_Headers != null)
                {
                    foreach (var kvp in _Headers.Data)
                    {
                        var key = kvp.Key;
                        var val = (kvp.Value ?? "").ToString();
                        if (key.IndexOfAny(new[] { '\r', '\n', ':', }) >= 0)
                        {
                            continue; // it is dangerous, may be attacking.
                        }
                        if (val.IndexOfAny(new[] { '\r', '\n', }) >= 0)
                        {
                            continue; // it is dangerous, may be attacking.
                        }
                        else
                        {
                            _InnerReq.SetRequestHeader(key, val);
                        }
                    }
                }
                if (_RangeEnabled)
                {
                    long filepos = 0;
                    if (_Dest != null)
                    {
                        using (var stream = PlatDependant.OpenRead(_Dest))
                        {
                            if (stream != null)
                            {
                                try
                                {
                                    filepos = stream.Length;
                                }
                                catch (Exception e)
                                {
                                    PlatDependant.LogError(e);
                                }
                            }
                        }
                    }
                    if (filepos <= 0)
                    {
                        if (_DestStream != null)
                        {
                            try
                            {
                                if (_DestStream.CanSeek)
                                {
                                    filepos = _DestStream.Length;
                                }
                            }
                            catch (Exception e)
                            {
                                PlatDependant.LogError(e);
                            }
                        }
                    }
                    if (filepos > 0)
                    {
                        if (filepos > int.MaxValue)
                        {
                            _RangeEnabled = false;
                        }
                        else
                        {
                            _InnerReq.SetRequestHeader("Range", "bytes=" + filepos + "-");
                        }
                    }
                    else
                    {
                        _RangeEnabled = false;
                    }
                }
                if (_Data != null && data != null)
                {
                    _InnerReq.method = "POST";
                    if (_Data.ContentType != null)
                    {
                        _InnerReq.SetRequestHeader("Content-Type", _Data.ContentType);
                    }
                    //_InnerReq.SetRequestHeader("Content-Length", data.Length.ToString());
                    _InnerReq.uploadHandler = new UploadHandlerRaw(data);
                }

                if (_Dest != null)
                {
                    if (_RangeEnabled)
                    {
                        _FinalDestStream = PlatDependant.OpenAppend(_Dest);
                        _Length = _Total = _DestStartOffset = (ulong)_FinalDestStream.Length;
                    }
                    else
                    {
                        _FinalDestStream = PlatDependant.OpenWrite(_Dest);
                    }
                }
                if (_FinalDestStream == null)
                {
                    if (_DestStream != null)
                    {
                        if (_RangeEnabled)
                        {
                            _DestStream.Seek(0, SeekOrigin.End);
                            _Length = _Total = _DestStartOffset = (ulong)_DestStream.Length;
                        }
                        _FinalDestStream = _DestStream;
                    }
                    else
                    {
                        //_ToMem = true;
                        _FinalDestStream = new MemoryStream();
                    }
                }

                _InnerReq.downloadHandler = new DownloadHandler(this);

                _InnerReq.disposeUploadHandlerOnDispose = true;
                _InnerReq.disposeDownloadHandlerOnDispose = true;
                _InnerReq.SendWebRequest();

                if (!ToMem)
                {
                    _IsBackgroundIORunning = true;
#if NETFX_CORE
                    var task = new System.Threading.Tasks.Task(BackgroundIOWork, null);
                    task.Start();
#else
                    System.Threading.ThreadPool.QueueUserWorkItem(BackgroundIOWork);
#endif
                }

                CoroutineRunner.StartCoroutine(WaitForDone());
            }
        }

        protected volatile bool _IsBackgroundIORunning = false;
        protected void BackgroundIOWork(object state)
        {
            try
            {
                byte[] buffer = PlatDependant.CopyStreamBuffer;
                var len = _Length;
                int readcnt = 0;
                while ((readcnt = _ReceiveStream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    _FinalDestStream.Write(buffer, 0, readcnt);
                    _FinalDestStream.Flush();
                    len += (ulong)readcnt;
                    _Length = len;
                }
            }
            finally
            {
                _IsBackgroundIORunning = false;
            }
        }
        protected IEnumerator WaitForDone()
        {
            while (!_InnerReq.isDone || _IsBackgroundIORunning)
            {
                yield return null;
            }
            FinishResponse();
        }

        protected void FinishResponse()
        {
            if (_Status != HttpRequestStatus.Finished)
            {
                if (_Error == null)
                {
                    if (_InnerReq == null)
                    {
                        _Error = "Request Error (Not Started)";
                    }
                    else
                    {
                        if (_InnerReq.error != null)
                        {
                            if (_InnerReq.isHttpError)
                            {
                                _Error = "HttpError: " + _InnerReq.responseCode + "\n" + _InnerReq.error;
                            }
                            else
                            {
                                _Error = _InnerReq.error;
                            }
                        }
                        else
                        {
                            var rawHeaders = _InnerReq.GetResponseHeaders();
                            _RespHeaders = new HttpRequestData();
                            foreach (var kvp in rawHeaders)
                            {
                                _RespHeaders.Add(kvp.Key, kvp.Value);
                            }
                            if (_RangeEnabled)
                            {
                                bool rangeRespFound = false;
                                foreach (var key in rawHeaders.Keys)
                                {
                                    if (key.ToLower() == "content-range")
                                    {
                                        rangeRespFound = true;
                                    }
                                }
                                if (!rangeRespFound)
                                {
                                    _RangeEnabled = false;
                                }
                            }
                            if (ToMem)
                            {
                                _Resp = new byte[_ReceiveStream.BufferedSize];
                                _ReceiveStream.Read(_Resp, 0, _Resp.Length);
                            }
                            else if (_DestStartOffset > 0 && !_RangeEnabled)
                            {
                                // Server does not support Range? What the hell...
                                try
                                {
                                    var realLength = _Length - _DestStartOffset;
                                    var buffer = PlatDependant.CopyStreamBuffer;
                                    for (ulong pos = 0; pos < realLength; pos += (ulong)buffer.Length)
                                    {
                                        _FinalDestStream.Seek((long)(pos + _DestStartOffset), SeekOrigin.Begin);
                                        var readcnt = _FinalDestStream.Read(buffer, 0, buffer.Length);
                                        if (readcnt > 0)
                                        {
                                            _FinalDestStream.Seek((long)pos, SeekOrigin.Begin);
                                            _FinalDestStream.Write(buffer, 0, readcnt);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    _FinalDestStream.SetLength((long)realLength);
                                }
                                catch (Exception)
                                {
                                    _Error = "Server does not support Range.";
                                }
                            }
                        }
                        _InnerReq.Dispose();
                    }
                }

                if (!ToExternal && _FinalDestStream != null)
                {
                    _FinalDestStream.Dispose();
                    _FinalDestStream = null;
                }
                _ReceiveStream.Dispose();
                _Status = HttpRequestStatus.Finished;
                if (_OnDone != null)
                {
                    var ondone = _OnDone;
                    _OnDone = null;
                    ondone();
                }
            }
        }

        public override void StopRequest()
        {
            if (_Status != HttpRequestStatus.Finished)
            {
                if (_InnerReq != null)
                {
                    _InnerReq.Abort();
                    _InnerReq.Dispose();
                }
                _ReceiveStream.Dispose();
                _Error = "Request Error (Cancelled)";
                FinishResponse();
            }
        }
    }

    internal partial class HttpRequestCreator
    {
        protected static HttpRequestCreator _Creator_Unity = new HttpRequestCreator("unity", (url, headers, data, dest) => new HttpRequest(url, headers, data, dest));
    }
}

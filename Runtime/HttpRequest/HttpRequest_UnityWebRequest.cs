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
        static HttpRequest()
        {
#if DISABLE_HTTPS_CERT_VERIFY
            IgnoreCertVerify = true;
#endif
        }
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
            [UnityPreserve]
            protected override void ReceiveContentLengthHeader(ulong contentLength)
            {
                ulong originTotal;
                if (_Req._DestExistingLength == null)
                {
                    _Req._DestExistingLength = originTotal = _Req._Total;
                }
                else
                {
                    originTotal = (ulong)_Req._DestExistingLength;
                }
                _Req._Total = originTotal + contentLength;
            }
            [UnityPreserve]
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
        protected class IgnoredCertVerifier : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                return true;
            }
        }

        public static bool IgnoreCertVerify = false;
        protected System.IO.Stream _FinalDestStream;
        protected ulong? _DestExistingLength = 0;
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
                    if (!_RangeEnabled)
                    {
                        if (_DestStream != null)
                        {
                            try
                            {
                                _DestStream.SetLength(0);
                            }
                            catch (Exception e)
                            {
                                PlatDependant.LogError(e);
                            }
                        }
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
                    if (data.RawData != null)
                    {
                        _InnerReq.uploadHandler = new UploadHandlerRaw(data);
                    }
                    else if (data.FilePath != null)
                    {
                        _InnerReq.uploadHandler = new UploadHandlerFile(data.FilePath);
                    }
                    else if (data.DataStream != null)
                    {
                        var tmpfile = ThreadSafeValues.UpdatePath + "/upload.file";
                        using (var tmpstream = PlatDependant.OpenWrite(tmpfile))
                        {
                            data.DataStream.CopyTo(tmpstream);
                        }
                        _InnerReq.uploadHandler = new UploadHandlerFile(tmpfile);
                    }
                }

                _DestStartOffset = 0;
                if (_Dest != null)
                {
                    if (_RangeEnabled)
                    {
                        _FinalDestStream = PlatDependant.OpenReadWrite(_Dest);
                        _FinalDestStream.Seek(0, SeekOrigin.End);
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
                if (IgnoreCertVerify)
                {
                    _InnerReq.certificateHandler = new IgnoredCertVerifier();
                }

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
            while (_Status != HttpRequestStatus.Finished && (!_InnerReq.isDone || _IsBackgroundIORunning))
            {
                if (_InnerReq.isDone && _IsBackgroundIORunning)
                {
                    _ReceiveStream.FinishWrite();
                }
                yield return null;
            }
            FinishResponse();
        }

        protected void FinishResponse()
        {
            if (_Status != HttpRequestStatus.Finished)
            {
                if (_RangeEnabled && _InnerReq != null && _InnerReq.isHttpError && _InnerReq.responseCode == (int)HttpStatusCode.RequestedRangeNotSatisfiable) // 416
                {
                    //PlatDependant.LogError("Server does not support Range.");
                    //try
                    //{
                    //    _RangeEnabled = false;
                    //    _Status = HttpRequestStatus.NotStarted;
                    //    if (_DestStream != null)
                    //    {
                    //        _DestStream.Seek(0, SeekOrigin.Begin);
                    //        _DestStream.SetLength(0);
                    //    }
                    //    if (!ToExternal && _FinalDestStream != null)
                    //    {
                    //        _FinalDestStream.Dispose();
                    //    }
                    //    if (_ReceiveStream != null)
                    //    {
                    //        _ReceiveStream.Dispose();
                    //    }
                    //    _ReceiveStream = new BidirectionMemStream();
                    //    StartRequest();
                    //    return;
                    //}
                    //catch (Exception e) { }

                    // Normally this is a fully downloaded file.
                    _InnerReq.Dispose();
                    _InnerReq = null; // https://forum.unity.com/threads/argumentnullexception-appear-randomly-in-unitywebrequest.541629/
                }
                else if (_Error == null)
                {
                    if (_InnerReq == null)
                    {
                        _Error = "Request Error (Not Started)";
                    }
                    else
                    {
                        if (_InnerReq.error != null && !_InnerReq.isHttpError)
                        {
                            if (_InnerReq.error.StartsWith("Request timeout", StringComparison.InvariantCultureIgnoreCase))
                            {
                                _Error = "timedout";
                            }
                            else
                            {
                                _Error = _InnerReq.error;
                            }
                        }
                        else
                        {
                            if (_InnerReq.isHttpError)
                            {
                                _Error = "HttpError: " + _InnerReq.responseCode + "\n" + _InnerReq.error;
                            }
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
                                    if (key.ToLower() == "accept-ranges")
                                    {
                                        if (rawHeaders[key].ToLower() == "bytes")
                                        {
                                            rangeRespFound = true;
                                        }
                                    }
                                    else if (key.ToLower() == "content-range")
                                    {
                                        rangeRespFound = true;
                                        //var headerval = rawHeaders[key].ToLower();
                                        //if (string.IsNullOrEmpty(headerval))
                                        //{
                                        //    rangeRespFound = false;
                                        //}
                                        //else
                                        //{
                                        //    var parts = headerval.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
                                        //    if (parts.Length < 1)
                                        //    {
                                        //        rangeRespFound = false;
                                        //    }
                                        //    else if (parts[0] != "bytes")
                                        //    {
                                        //        rangeRespFound = false;
                                        //    }
                                        //    else if (parts.Length > 1)
                                        //    {
                                        //        ulong respstart;
                                        //        if (!ulong.TryParse(parts[1], out respstart))
                                        //        {
                                        //            rangeRespFound = false;
                                        //        }
                                        //        else if (respstart != _DestStartOffset)
                                        //        {
                                        //            rangeRespFound = false;
                                        //        }
                                        //    }
                                        //}
                                        //if (!rangeRespFound)
                                        //{
                                        //    break;
                                        //}
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
                                    _FinalDestStream.Flush();
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
                                catch (Exception e)
                                {
                                    _Error = "Server does not support Range.";
                                    PlatDependant.LogError(_Error);
                                    PlatDependant.LogError(e);
                                    try
                                    {
                                        _FinalDestStream.Seek(0, SeekOrigin.Begin);
                                        _FinalDestStream.SetLength(0);
                                    }
                                    catch { }
                                }
                            }
                        }
                        _InnerReq.Dispose();
                        _InnerReq = null; // https://forum.unity.com/threads/argumentnullexception-appear-randomly-in-unitywebrequest.541629/
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
                    _InnerReq = null; // https://forum.unity.com/threads/argumentnullexception-appear-randomly-in-unitywebrequest.541629/
                }
                _ReceiveStream.Dispose();
                _Error = "cancelled";
                FinishResponse();
            }
        }
    }

    internal partial class HttpRequestCreator
    {
        protected static HttpRequestCreator _Creator_Unity = new HttpRequestCreator("unity", (url, headers, data, dest) => new HttpRequest(url, headers, data, dest));
    }
}

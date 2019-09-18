#if UNITY_IOS
#define HTTP_REQ_DONOT_ABORT
#endif

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public class HttpRequestLegacy : HttpRequestBase
    {
        protected System.Net.HttpWebRequest _InnerReq;
        protected object _CloseLock = new object();
        protected bool _Closed = false;
#if HTTP_REQ_DONOT_ABORT
        protected List<IDisposable> _CloseList = new List<IDisposable>();
#endif

        static HttpRequestLegacy()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) => true;
            System.Net.ServicePointManager.DefaultConnectionLimit = int.MaxValue;
        }

        public HttpRequestLegacy(string url, HttpRequestData headers, HttpRequestData data, string dest)
            : base(url, headers, data, dest)
        {
        }
        public HttpRequestLegacy(string url, HttpRequestData data, string dest)
            : this(url, null, data, dest)
        {
        }
        public HttpRequestLegacy(string url, string dest)
            : this(url, null, null, dest)
        {
        }
        public HttpRequestLegacy(string url, HttpRequestData data)
            : this(url, null, data, null)
        {
        }
        public HttpRequestLegacy(string url)
            : this(url, null, null, null)
        {
        }
        
        public override void StartRequest()
        {
            if (_Status == HttpRequestStatus.NotStarted)
            {
                _Status = HttpRequestStatus.Running;
#if NETFX_CORE
                var task = new System.Threading.Tasks.Task(RequestWork, null);
                task.Start();
#else
                System.Threading.ThreadPool.QueueUserWorkItem(RequestWork);
#endif
#if HTTP_REQ_DONOT_ABORT
    			if (_Timeout > 0)
    			{
    				System.Threading.ThreadPool.QueueUserWorkItem(state =>
    				{
    					System.Threading.Thread.Sleep(_Timeout);
    					StopRequest();
    				});
    			}
#endif
            }
        }

        public override void StopRequest()
        {
            lock (_CloseLock)
            {
                if (!_Closed)
                {
                    _Closed = true;
                    if (_InnerReq != null)
                    {
                        var req = _InnerReq;
                        _InnerReq = null;
#if HTTP_REQ_DONOT_ABORT
                        foreach(var todispose in _CloseList)
                        {
                            if (todispose != null)
                            {
                                todispose.Dispose();
                            }
                        }
#else
                        req.Abort();
#endif
                        if (_Error == null)
                        {
                            _Error = "timedout";
                        }
                        _Status = HttpRequestStatus.Finished;
                        if (_OnDone != null)
                        {
                            var ondone = _OnDone;
                            _OnDone = null;
                            ondone();
                        }
                    }
                }
            }
        }

        private volatile static System.Reflection.FieldInfo _hostField;
        private volatile static System.Reflection.FieldInfo _hostFieldLock;
        public void RequestWork(object state)
        {
            try
            {
                var uri = new Uri(_Url);
#if NETFX_CORE
                System.Net.HttpWebRequest req = System.Net.HttpWebRequest.CreateHttp(uri);
#else
#if (UNITY_5 || UNITY_5_3_OR_NEWER) && UNITY_EDITOR
                System.Net.HttpWebRequest req = System.Net.HttpWebRequest.Create(uri) as System.Net.HttpWebRequest;
#else
                System.Net.HttpWebRequest req = new System.Net.HttpWebRequest(uri);
#endif
                req.KeepAlive = false;

                try
                {
                    // https://stackoverflow.com/questions/15643223/dns-refresh-timeout-with-mono
                    // Clear out the cached host entry
                    if (_hostField == null || _hostFieldLock == null)
                    {
                        _hostField = typeof(System.Net.ServicePoint).GetField("host", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        _hostFieldLock = typeof(System.Net.ServicePoint).GetField("hostE", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    }
                    var hostLock = _hostFieldLock.GetValue(req.ServicePoint);
                    lock (hostLock)
                        _hostField.SetValue(req.ServicePoint, null);
                }
                catch (Exception re)
                {
                    PlatDependant.LogError(re);
                }
#endif

                try
                {
                    lock (_CloseLock)
                    {
                        if (_Closed)
                        {
#if !HTTP_REQ_DONOT_ABORT
                            req.Abort();
#endif
                            if (_Status != HttpRequestStatus.Finished)
                            {
                                _Error = "Request Error (Cancelled)";
                                _Status = HttpRequestStatus.Finished;
                            }
                            return;
                        }
                        _InnerReq = req;
                    }

#if !NETFX_CORE
                    req.Timeout = int.MaxValue;
                    req.ReadWriteTimeout = int.MaxValue;
#if !HTTP_REQ_DONOT_ABORT
                    if (_Timeout > 0)
                    {
                        req.Timeout = _Timeout;
                        req.ReadWriteTimeout = _Timeout;

                    }
#endif
#endif

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
                                req.Headers[key] = val;
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
                                req.AddRange((int)filepos);
                            }
                        }
                        else
                        {
                            _RangeEnabled = false;
                        }
                    }
                    if (_Data != null && data != null)
                    {
                        req.Method = "POST";
                        if (_Data.ContentType != null)
                        {
                            req.ContentType = _Data.ContentType;
                        }

#if NETFX_CORE
                        var tstream = req.GetRequestStreamAsync();
                        if (_Timeout > 0)
                        {
                            if (!tstream.Wait(_Timeout))
                            {
                                throw new TimeoutException();
                            }
                        }
                        else
                        {
                            tstream.Wait();
                        }
                        var stream = tstream.Result;
#else
                        req.ContentLength = data.Length;
                        var stream = req.GetRequestStream();
#endif

                        lock (_CloseLock)
                        {
                            if (_Closed)
                            {
#if !HTTP_REQ_DONOT_ABORT
                                req.Abort();
#endif
                                if (_Status != HttpRequestStatus.Finished)
                                {
                                    _Error = "Request Error (Cancelled)";
                                    _Status = HttpRequestStatus.Finished;
                                }
                                return;
                            }
                        }
                        if (stream != null)
                        {
#if NETFX_CORE
                            if (_Timeout > 0)
                            {
                                stream.WriteTimeout = _Timeout;
                            }
                            else
                            {
                                stream.WriteTimeout = int.MaxValue;
                            }
#endif

                            try
                            {
                                stream.Write(data, 0, data.Length);
                                stream.Flush();
                            }
                            finally
                            {
                                stream.Dispose();
                            }
                        }
                    }
                    else
                    {
                    }
                    lock (_CloseLock)
                    {
                        if (_Closed)
                        {
#if !HTTP_REQ_DONOT_ABORT
                            req.Abort();
#endif
                            if (_Status != HttpRequestStatus.Finished)
                            {
                                _Error = "Request Error (Cancelled)";
                                _Status = HttpRequestStatus.Finished;
                            }
                            return;
                        }
                    }
#if NETFX_CORE
                    var tresp = req.GetResponseAsync();
                    if (_Timeout > 0)
                    {
                        if (!tresp.Wait(_Timeout))
                        {
                            throw new TimeoutException();
                        }
                    }
                    else
                    {
                        tresp.Wait();
                    }
                    var resp = tresp.Result;
#else
                    var resp = req.GetResponse();
#endif
                    lock (_CloseLock)
                    {
                        if (_Closed)
                        {
#if !HTTP_REQ_DONOT_ABORT
                            req.Abort();
#endif
                            if (_Status != HttpRequestStatus.Finished)
                            {
                                _Error = "Request Error (Cancelled)";
                                _Status = HttpRequestStatus.Finished;
                            }
                            return;
                        }
                    }
                    if (resp != null)
                    {
                        try
                        {
                            _Total = (ulong)resp.ContentLength;
                        }
                        catch
                        {
                        }
                        try
                        {
                            _RespHeaders = new HttpRequestData();
                            foreach (var key in resp.Headers.AllKeys)
                            {
                                _RespHeaders.Add(key, resp.Headers[key]);
                            }

                            if (_RangeEnabled)
                            {
                                bool rangeRespFound = false;
                                foreach (var key in resp.Headers.AllKeys)
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

                            var stream = resp.GetResponseStream();
                            lock (_CloseLock)
                            {
                                if (_Closed)
                                {
#if !HTTP_REQ_DONOT_ABORT
                                    req.Abort();
#endif
                                    if (_Status != HttpRequestStatus.Finished)
                                    {
                                        _Error = "Request Error (Cancelled)";
                                        _Status = HttpRequestStatus.Finished;
                                    }
                                    return;
                                }
                            }
                            if (stream != null)
                            {
#if NETFX_CORE
                                if (_Timeout > 0)
                                {
                                    stream.ReadTimeout = _Timeout;
                                }
                                else
                                {
                                    stream.ReadTimeout = int.MaxValue;
                                }
#endif
                                Stream streamd = null;
                                try
                                {
                                    byte[] buffer = new byte[64 * 1024];
                                    ulong totalcnt = 0;
                                    int readcnt = 0;

                                    bool mem = false;
                                    if (_Dest != null)
                                    {
                                        if (_RangeEnabled)
                                        {
                                            streamd = PlatDependant.OpenAppend(_Dest);
                                            totalcnt = (ulong)streamd.Length;
                                        }
                                        else
                                        {
                                            streamd = PlatDependant.OpenWrite(_Dest);
                                        }
#if HTTP_REQ_DONOT_ABORT
                                        if (streamd != null)
                                        {
                                            _CloseList.Add(streamd);
                                        }
#endif
                                    }
                                    if (streamd == null)
                                    {
                                        if (_DestStream != null)
                                        {
                                            if (_RangeEnabled)
                                            {
                                                _DestStream.Seek(0, SeekOrigin.End);
                                                totalcnt = (ulong)_DestStream.Length;
                                            }
                                            streamd = _DestStream;
                                        }
                                        else
                                        {
                                            mem = true;
                                            streamd = new MemoryStream();
#if HTTP_REQ_DONOT_ABORT
                                            _CloseList.Add(streamd);
#endif
                                        }
                                    }

                                    if (_Total > 0)
                                    {
                                        _Total += totalcnt;
                                    }

                                    do
                                    {
                                        lock (_CloseLock)
                                        {
                                            if (_Closed)
                                            {
#if !HTTP_REQ_DONOT_ABORT
                                                req.Abort();
#endif
                                                if (_Status != HttpRequestStatus.Finished)
                                                {
                                                    _Error = "Request Error (Cancelled)";
                                                    _Status = HttpRequestStatus.Finished;
                                                }
                                                return;
                                            }
                                        }
                                        try
                                        {
                                            readcnt = 0;
                                            readcnt = stream.Read(buffer, 0, buffer.Length);
                                            if (readcnt <= 0)
                                            {
                                                stream.ReadByte(); // when it is closed, we need read to raise exception.
                                                break;
                                            }

                                            streamd.Write(buffer, 0, readcnt);
                                            streamd.Flush();
                                        }
                                        catch (TimeoutException te)
                                        {
                                            PlatDependant.LogError(te);
                                            _Error = "timedout";
                                        }
                                        catch (System.Net.WebException we)
                                        {
                                            PlatDependant.LogError(we);
#if NETFX_CORE
                                            if (we.Status.ToString() == "Timeout")
#else
                                            if (we.Status == System.Net.WebExceptionStatus.Timeout)
#endif
                                            {
                                                _Error = "timedout";
                                            }
                                            else
                                            {
                                                _Error = "Request Error (Exception):\n" + we.ToString();
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            PlatDependant.LogError(e);
                                            _Error = "Request Error (Exception):\n" + e.ToString();
                                        }
                                        lock (_CloseLock)
                                        {
                                            if (_Closed)
                                            {
#if !HTTP_REQ_DONOT_ABORT
                                                req.Abort();
#endif
                                                if (_Status != HttpRequestStatus.Finished)
                                                {
                                                    _Error = "Request Error (Cancelled)";
                                                    _Status = HttpRequestStatus.Finished;
                                                }
                                                return;
                                            }
                                        }
                                        totalcnt += (ulong)readcnt;
                                        _Length = totalcnt;
                                        //Capstones.PlatExt.PlatDependant.LogInfo(readcnt);
                                    } while (readcnt > 0);

                                    if (mem)
                                    {
                                        _Resp = ((MemoryStream)streamd).ToArray();
                                    }
                                }
                                finally
                                {
                                    stream.Dispose();
                                    if (streamd != null)
                                    {
                                        if (streamd != _DestStream)
                                        {
                                            streamd.Dispose();
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
#if NETFX_CORE
                            resp.Dispose();
#else
                            resp.Close();
#endif
                        }
                    }
                }
                catch (TimeoutException te)
                {
                    PlatDependant.LogError(te);
                    _Error = "timedout";
                }
                catch (System.Net.WebException we)
                {
                    PlatDependant.LogError(we);
#if NETFX_CORE
                    if (we.Status.ToString() == "Timeout")
#else
                    if (we.Status == System.Net.WebExceptionStatus.Timeout)
#endif
                    {
                        _Error = "timedout";
                    }
                    else
                    {
                        if (we.Response is System.Net.HttpWebResponse && ((System.Net.HttpWebResponse)we.Response).StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                        {

                        }
                        else
                        {
                            _Error = "Request Error (Exception):\n" + we.ToString();
                        }
                    }
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                    _Error = "Request Error (Exception):\n" + e.ToString();
                }
                finally
                {
                    if (_Error == null)
                    {
                        lock (_CloseLock)
                        {
                            _Closed = true;
                            _InnerReq = null;
                        }
                    }
                    else
                    {
                        StopRequest();
                    }
                }
            }
            catch (TimeoutException te)
            {
                PlatDependant.LogError(te);
                _Error = "timedout";
            }
            catch (System.Net.WebException we)
            {
                PlatDependant.LogError(we);
#if NETFX_CORE
                if (we.Status.ToString() == "Timeout")
#else
                if (we.Status == System.Net.WebExceptionStatus.Timeout)
#endif
                {
                    _Error = "timedout";
                }
                else
                {
                    _Error = "Request Error (Exception):\n" + we.ToString();
                }
            }
            catch (Exception e)
            {
                PlatDependant.LogError(e);
                _Error = "Request Error (Exception):\n" + e.ToString();
            }
            finally
            {
                lock (_CloseLock)
                {
                    _Status = HttpRequestStatus.Finished;
                    if (_OnDone != null)
                    {
                        var ondone = _OnDone;
                        _OnDone = null;
                        ondone();
                    }
                }
            }
        }
    }
}

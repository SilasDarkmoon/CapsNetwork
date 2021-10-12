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
using Capstones.UnityEngineEx;

#if (UNITY_ENGINE || UNITY_5_3_OR_NEWER) && !NET_4_6 && !NET_STANDARD_2_0
using Unity.IO.Compression;
#else
using System.IO.Compression;
#endif

namespace Capstones.Net
{
    public class HttpRequestData
    {
        protected Dictionary<string, object> _Data = new Dictionary<string, object>();
        public Dictionary<string, object> Data
        {
            get { return _Data; }
        }

        public string ContentType;

        public byte[] Encoded = null;

        public void Add(string key, object val)
        {
            if (key != null)
            {
                _Data[key] = val;
            }
        }

        public void Remove(string key)
        {
            _Data.Remove(key);
        }

        public int Count
        {
            get { return _Data.Count; }
        }

        protected string _CompressMethod;
        protected string _EncryptMethod;
        protected string _PrepareMethod;
        public string CompressMethod
        {
            get
            {
                return _CompressMethod ?? HttpRequestBase.PreferredCompressMethod;
            }
            set
            {
                _CompressMethod = value;
            }
        }
        public string EncryptMethod
        {
            get
            {
                return _EncryptMethod ?? HttpRequestBase.PreferredEncryptMethod;
            }
            set
            {
                _EncryptMethod = value;
            }
        }
        public string PrepareMethod
        {
            get
            {
                return _PrepareMethod ?? HttpRequestBase.PreferredPrepareMethod;
            }
            set
            {
                _PrepareMethod = value;
            }
        }
    }

    public enum HttpRequestStatus
    {
        NotStarted = 0,
        Running,
        Finished,
    }

    internal partial class HttpRequestCreator
    {
        protected static Dictionary<string, HttpRequestCreator> _Creators;
        protected static Dictionary<string, HttpRequestCreator> Creators
        {
            get
            {
                var creators = _Creators;
                if (creators == null)
                {
                    _Creators = creators = new Dictionary<string, HttpRequestCreator>();
                }
                return creators;
            }
        }
        public delegate HttpRequestBase CreateFunc(string url, HttpRequestData headers, HttpRequestData data, string dest);
        public static HttpRequestBase Create(string name, string url, HttpRequestData headers, HttpRequestData data, string dest)
        {
            HttpRequestCreator creator;
            if (name != null)
            {
                if (Creators.TryGetValue(name, out creator))
                {
                    return creator.Create(url, headers, data, dest);
                }
            }
            return null;
        }

        public string Name { get; protected set; }
        public CreateFunc Creator { get; protected set; }
        public HttpRequestBase Create(string url, HttpRequestData headers, HttpRequestData data, string dest)
        {
            var func = Creator;
            if (func != null)
            {
                return func(url, headers, data, dest);
            }
            return null;
        }
        protected HttpRequestCreator(string name, CreateFunc func)
        {
            Name = name;
            Creator = func;
            Creators[name] = this;
        }
    }

    public abstract class HttpRequestBase : CustomYieldInstruction
    {
        protected HttpRequestData _Headers = null;
        protected HttpRequestData _Data = null;
        protected string _Url = null;
        protected HttpRequestStatus _Status = HttpRequestStatus.NotStarted;
        protected string _Error = null;
        protected string _Dest = null;
        protected Stream _DestStream = null;
        protected Action _OnDone = null;
        protected ulong _Length = 0;
        protected ulong _Total = 0;
        protected int _Timeout = -1;
        protected bool _RangeEnabled = false;

        protected byte[] _Resp = null;
        protected HttpRequestData _RespHeaders = null;

        public HttpRequestBase(string url, HttpRequestData headers, HttpRequestData data, string dest)
        {
            _Url = url;
            _Headers = headers;
            _Data = data;
            _Dest = dest;
        }
        public HttpRequestBase(string url, HttpRequestData data, string dest)
            : this(url, null, data, dest)
        {
        }
        public HttpRequestBase(string url, string dest)
            : this(url, null, null, dest)
        {
        }
        public HttpRequestBase(string url, HttpRequestData data)
            : this(url, null, data, null)
        {
        }
        public HttpRequestBase(string url)
            : this(url, null, null, null)
        {
        }

        public override string ToString()
        {
            return (_Url ?? "http://<null>") + "\n" + GetType().ToString();
        }

        public int Timeout
        {
            get { return _Timeout; }
            set
            {
                if (_Status == HttpRequestStatus.NotStarted)
                {
                    _Timeout = value;
                }
                else
                {
                    throw new InvalidOperationException("Cannot change request parameters after it is started.");
                }
            }
        }
        public Stream DestStream
        {
            get { return _DestStream; }
            set
            {
                if (_Status == HttpRequestStatus.NotStarted)
                {
                    _DestStream = value;
                }
                else
                {
                    throw new InvalidOperationException("Cannot change request parameters after it is started.");
                }
            }
        }
        public Action OnDone
        {
            get { return _OnDone; }
            set
            {
                if (_Status == HttpRequestStatus.NotStarted)
                {
                    _OnDone = value;
                }
                else
                {
                    throw new InvalidOperationException("Cannot change request parameters after it is started.");
                }
            }
        }
        public bool RangeEnabled
        {
            get { return _RangeEnabled; }
            set
            {
                if (_Status == HttpRequestStatus.NotStarted)
                {
                    _RangeEnabled = value;
                }
                else
                {
                    throw new InvalidOperationException("Cannot change request parameters after it is started.");
                }
            }
        }

        public override bool keepWaiting
        {
            get { return !IsDone; }
        }
        public bool IsDone
        {
            get { return _Status == HttpRequestStatus.Finished; }
        }
        public byte[] Result
        {
            get { return _Resp; }
        }
        public HttpRequestData RespHeaders
        {
            get { return _RespHeaders; }
        }
        public string Error
        {
            get { return _Error; }
        }
        public ulong Length
        {
            get { return _Length; }
        }
        public ulong Total
        {
            get { return _Total; }
        }

        public abstract void StartRequest();
        public abstract void StopRequest();

        public delegate byte[] DataPostProcessFunc(byte[] data, string token, ulong seq);
        public static readonly Dictionary<string, DataPostProcessFunc> CompressFuncs = new Dictionary<string, DataPostProcessFunc>();
        public static readonly Dictionary<string, DataPostProcessFunc> DecompressFuncs = new Dictionary<string, DataPostProcessFunc>();
        public static readonly Dictionary<string, DataPostProcessFunc> EncryptFuncs = new Dictionary<string, DataPostProcessFunc>();
        public static readonly Dictionary<string, DataPostProcessFunc> DecryptFuncs = new Dictionary<string, DataPostProcessFunc>();
        public static readonly HashSet<string> IgnoredCompressMethods = new HashSet<string>();
        public delegate void RequestDataPrepareFunc(HttpRequestData form, string token, ulong seq, HttpRequestData headers);
        public static readonly Dictionary<string, RequestDataPrepareFunc> RequestDataPrepareFuncs = new Dictionary<string, RequestDataPrepareFunc>();
        public static string PreferredCompressMethod;
        public static string PreferredEncryptMethod;
        public static string PreferredPrepareMethod;
        public static string PreferredImplement;

        protected void AddHeaderRaw(string key, string value)
        {
            if (_Headers == null)
            {
                _Headers = new HttpRequestData();
            }
            _Headers.Add(key, value);
        }
        public void AddHeader(string key, string value)
        {
            if (_Status == HttpRequestStatus.NotStarted)
            {
                AddHeaderRaw(key, value);
            }
            else
            {
                throw new InvalidOperationException("Cannot change request parameters after it is started.");
            }
        }
        public string Token
        {
            get
            {
                string token = null;
                if (_RespHeaders != null)
                {
                    foreach (var kvp in _RespHeaders.Data)
                    {
                        var lkey = kvp.Key.ToLower();
                        if (lkey == "t")
                        {
                            token = kvp.Value.ToString();
                            break;
                        }
                    }
                }
                if (_Headers != null)
                {
                    if (token == null)
                    {
                        foreach (var kvp in _Headers.Data)
                        {
                            var lkey = kvp.Key.ToLower();
                            if (lkey == "t")
                            {
                                token = kvp.Value.ToString();
                                break;
                            }
                        }
                    }
                }
                return token;
            }
            set
            {
                if (_Status == HttpRequestStatus.NotStarted)
                {
                    //if (_Data == null)
                    //{
                    //    _Data = new HttpRequestData();
                    //}
                    //_Data.Add("t", value);
                    if (_Headers == null)
                    {
                        _Headers = new HttpRequestData();
                    }
                    //_Headers.Add("UserToken", value);
                    _Headers.Add("t", value);
                }
                else
                {
                    throw new InvalidOperationException("Cannot change request parameters after it is started.");
                }
            }
        }
        /// <summary>
        /// this is the logic seq.
        /// </summary>
        public ulong Seq
        {
            get
            {
                ulong seq = 0;
                if (_RespHeaders != null)
                {
                    foreach (var kvp in _RespHeaders.Data)
                    {
                        var lkey = kvp.Key.ToLower();
                        if (lkey == "seq")
                        {
                            ulong.TryParse(kvp.Value.ToString(), out seq);
                            break;
                        }
                    }
                }
                if (_Headers != null)
                {
                    if (seq == 0)
                    {
                        foreach (var kvp in _Headers.Data)
                        {
                            var lkey = kvp.Key.ToLower();
                            if (lkey == "seq")
                            {
                                ulong.TryParse(kvp.Value.ToString(), out seq);
                                break;
                            }
                        }
                    }
                }
                return seq;
            }
            set
            {
                if (_Status == HttpRequestStatus.NotStarted)
                {
                    //if (_Data == null)
                    //{
                    //    _Data = new HttpRequestData();
                    //}
                    //_Data.Add("seq", value.ToString());
                    if (_Headers == null)
                    {
                        _Headers = new HttpRequestData();
                    }

                    _Headers.Add("Seq", value.ToString());
                }
                else
                {
                    throw new InvalidOperationException("Cannot change request parameters after it is started.");
                }
            }
        }
        /// <summary>
        /// this is the raw seq.
        /// </summary>
        public ulong RSeq
        {
            get
            {
                ulong seq = 0;
                if (_RespHeaders != null)
                {
                    foreach (var kvp in _RespHeaders.Data)
                    {
                        var lkey = kvp.Key.ToLower();
                        if (lkey == "rseq")
                        {
                            ulong.TryParse(kvp.Value.ToString(), out seq);
                            break;
                        }
                    }
                }
                if (_Headers != null)
                {
                    if (seq == 0)
                    {
                        foreach (var kvp in _Headers.Data)
                        {
                            var lkey = kvp.Key.ToLower();
                            if (lkey == "rseq")
                            {
                                ulong.TryParse(kvp.Value.ToString(), out seq);
                                break;
                            }
                        }
                    }
                }
                return seq;
            }
            set
            {
                if (_Status == HttpRequestStatus.NotStarted)
                {
                    //if (_Data == null)
                    //{
                    //    _Data = new HttpRequestData();
                    //}
                    //_Data.Add("rseq", value.ToString());
                    if (_Headers == null)
                    {
                        _Headers = new HttpRequestData();
                    }
                    _Headers.Add("RSeq", value.ToString());
                }
                else
                {
                    throw new InvalidOperationException("Cannot change request parameters after it is started.");
                }
            }
        }
        public void ParseTokenAndSeq(out string token, out ulong seq)
        {
            token = null;
            seq = 0;
            if (_RespHeaders != null)
            {
                foreach (var kvp in _RespHeaders.Data)
                {
                    var lkey = kvp.Key.ToLower();
                    if (lkey == "seq")
                    {
                        ulong.TryParse(kvp.Value.ToString(), out seq);
                    }
                    else if (lkey == "t")
                    {
                        token = kvp.Value.ToString();
                    }
                }
            }
            if (_Headers != null)
            {
                if (token == null)
                {
                    foreach (var kvp in _Headers.Data)
                    {
                        var lkey = kvp.Key.ToLower();
                        if (lkey == "t")
                        {
                            token = kvp.Value.ToString();
                            break;
                        }
                    }
                }
                if (seq == 0)
                {
                    foreach (var kvp in _Headers.Data)
                    {
                        var lkey = kvp.Key.ToLower();
                        if (lkey == "seq")
                        {
                            ulong.TryParse(kvp.Value.ToString(), out seq);
                            break;
                        }
                    }
                }
            }
        }

        public byte[] ParseResponse()
        {
            string token;
            ulong seq;
            ParseTokenAndSeq(out token, out seq);
            return ParseResponse(token, seq);
        }
        public byte[] ParseResponse(string token, ulong seq)
        {
            string error;
            var data = ParseResponse(token, seq, out error);
            if (error != null)
            {
                PlatDependant.LogError(error);
            }
            return data;
        }
        public byte[] ParseResponse(string token, ulong seq, out string error)
        {
            if (!IsDone)
            {
                error = "Request undone.";
                return null;
            }
            else
            {
                if (!string.IsNullOrEmpty(Error) && !Error.StartsWith("HttpError: "))
                {
                    error = Error;
                    return null;
                }
                else
                {
                    string enc = "";
                    bool encrypted = false;
                    string encryptmethod = "";
                    if (_RespHeaders != null)
                    {
                        foreach (var kvp in _RespHeaders.Data)
                        {
                            var lkey = kvp.Key.ToLower();
                            if (lkey == "content-encoding")
                            {
                                enc = kvp.Value.ToString().ToLower();
                            }
                            else if (lkey == "encrypted")
                            {
                                var val = kvp.Value.ToString();
                                if (val != null) val = val.ToLower();
                                encrypted = !string.IsNullOrEmpty(val) && val != "n" && val != "0" && val != "f" &&
                                            val != "no" && val != "false";
                                if (encrypted)
                                {
                                    encryptmethod = val;
                                }
                            }
                        }
                    }

                    var data = _Resp;
                    if (!string.IsNullOrEmpty(enc) && !IgnoredCompressMethods.Contains(enc))
                    {
                        DataPostProcessFunc decompressFunc;
                        if (!DecompressFuncs.TryGetValue(enc, out decompressFunc))
                        {
                            error = "No decompressor for " + enc;
                            return null;
                        }
                        try
                        {
                            data = decompressFunc(data, token, seq);
                        }
                        catch (Exception e)
                        {
                            //error = e.ToString();
                            //return null;
                            PlatDependant.LogError("Decompress " + enc + " failed. Will ignore " + enc + ". Exception:\n" + e.ToString());
                            IgnoredCompressMethods.Add(enc);
                        }
                    }

                    if (encrypted)
                    {
                        DataPostProcessFunc decryptFunc;
                        if (!DecryptFuncs.TryGetValue(encryptmethod, out decryptFunc))
                        {
                            error = "No decrytor for " + encryptmethod;
                            return null;
                        }
                        try
                        {
                            data = decryptFunc(data, token, seq);
                        }
                        catch (Exception e)
                        {
                            error = e.ToString();
                            return null;
                        }
                    }

                    error = null;
                    return data;
                }
            }
        }
        public string ParseResponseText()
        {
            string token;
            ulong seq;
            ParseTokenAndSeq(out token, out seq);
            return ParseResponseText(token, seq);
        }
        public string ParseResponseText(string token, ulong seq)
        {
            string error;
            var data = ParseResponse(token, seq, out error);
            if (error != null)
            {
                return error;
            }
            if (data == null)
            {
                return null;
            }
            if (data.Length == 0)
            {
                return "";
            }
            try
            {
                var txt = System.Text.Encoding.UTF8.GetString(data, 0, data.Length);
                return txt;
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        public byte[] PrepareRequestData()
        {
            string token;
            ulong seq;
            ParseTokenAndSeq(out token, out seq);
            return PrepareRequestData(token, seq);
        }
        public byte[] PrepareRequestData(string token, ulong seq)
        {
            if (_Data == null)
            {
                return null;
            }
            RequestDataPrepareFunc prepareFunc;
            if (RequestDataPrepareFuncs.TryGetValue(_Data.PrepareMethod, out prepareFunc))
            {
                try
                {
                    prepareFunc(_Data, token, seq, _Headers);
                }
                catch (Exception e)
                {
                    PlatDependant.LogError(e);
                }
            }
            var data = _Data.Encoded;
            if (data == null)
            {
                return null;
            }
            var encryptMethod = _Data.EncryptMethod;
            if (!string.IsNullOrEmpty(encryptMethod))
            {
                DataPostProcessFunc encryptFunc;
                if (EncryptFuncs.TryGetValue(encryptMethod, out encryptFunc))
                {
                    try
                    {
                        data = encryptFunc(data, token, seq);
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                    }
                }
                else
                {
                    PlatDependant.LogError("no encryptor for " + encryptMethod);
                }
            }
            if (data != null)
            {
                AddHeaderRaw("Encrypted", encryptMethod);
            }
            else
            {
                data = _Data.Encoded;
            }
            var compressMethod = _Data.CompressMethod;
            if (!string.IsNullOrEmpty(compressMethod))
            {
                DataPostProcessFunc compressFunc;
                if (CompressFuncs.TryGetValue(compressMethod, out compressFunc))
                {
                    try
                    {
                        data = compressFunc(data, token, seq);
                    }
                    catch (Exception e)
                    {
                        PlatDependant.LogError(e);
                    }
                }
                else
                {
                    PlatDependant.LogError("no compressor for " + compressMethod);
                }
            }
            if (data != null)
            {
                if (!string.IsNullOrEmpty(compressMethod))
                {
                    AddHeaderRaw("Content-Encoding", compressMethod);
                    AddHeaderRaw("Accept-Encoding", compressMethod);
                }
            }
            else
            {
                data = _Data.Encoded;
            }
            return data;
        }

        public static class PrepareFuncHelper
        {
            public static JSONObject EncodeJson(Dictionary<string, object> data)
            {
                JSONObject obj = new JSONObject();
                foreach (var kvp in data)
                {
                    if (kvp.Value is bool)
                    {
                        obj.AddField(kvp.Key, (bool)kvp.Value);
                    }
                    else if (kvp.Value is int)
                    {
                        obj.AddField(kvp.Key, (int)kvp.Value);
                    }
                    else if (kvp.Value is float)
                    {
                        obj.AddField(kvp.Key, (float)kvp.Value);
                    }
                    else if (kvp.Value is double)
                    {
                        obj.AddField(kvp.Key, (double)kvp.Value);
                    }
                    else if (kvp.Value is string)
                    {
                        obj.AddField(kvp.Key, (string)kvp.Value);
                    }
                    else if (kvp.Value is Dictionary<string, object>)
                    {
                        obj.AddField(kvp.Key, EncodeJson(kvp.Value as Dictionary<string, object>));
                    }
                    else if (kvp.Value is Array)
                    {
                    }
                    //else if (kvp.Value == null)
                    //{
                    //    obj.AddField(kvp.Key, null);
                    //}
                }
                return obj;
            }
            public static void Prepare_Default(HttpRequestData form, string token, ulong seq, HttpRequestData headers)
            {
                if (form.Encoded != null)
                {
                    if (form.ContentType == null)
                    {
                        form.ContentType = "application/octet-stream";
                    }
                    return;
                }
                else
                {
                    List<byte> buffer = new List<byte>();
                    bool first = true;
                    foreach (var kvp in form.Data)
                    {
                        if (first)
                        {
                            first = false;
                        }
                        else
                        {
                            buffer.AddRange(Encoding.UTF8.GetBytes("&"));
                        }
                        buffer.AddRange(Encoding.UTF8.GetBytes(Uri.EscapeDataString(kvp.Key)));
                        buffer.AddRange(Encoding.UTF8.GetBytes("="));
                        if (kvp.Value != null)
                        {
                            buffer.AddRange(Encoding.UTF8.GetBytes(Uri.EscapeDataString(kvp.Value.ToString())));
                        }
                    }
                    var arr = buffer.ToArray();
                    form.Encoded = arr;
                    form.ContentType = "application/x-www-form-urlencoded";
                }
            }
            public static void Prepare_Json(HttpRequestData form, string token, ulong seq, HttpRequestData headers)
            {
                object jobj;
                if (form.Data.TryGetValue("", out jobj))
                {
                    string jstr = null;
                    if (jobj is string)
                    {
                        jstr = jobj as string;
                    }
                    else if (jobj is JSONObject)
                    {
                        jstr = ((JSONObject)jobj).ToString();
                    }
                    if (jstr != null)
                    {
                        form.Encoded = Encoding.UTF8.GetBytes(jstr);
                        form.ContentType = "application/json";
                    }
                }
            }
            public static void Prepare_Form2Json(HttpRequestData form, string token, ulong seq, HttpRequestData headers)
            {
                var jobj = EncodeJson(form.Data);
                if (jobj != null)
                {
                    form.Encoded = Encoding.UTF8.GetBytes(jobj.ToString());
                    form.ContentType = "application/json";
                }
            }

            public static byte[] CompressFunc_GZip(byte[] data, string token, ulong seq)
            {
                var memstream = new MemoryStream();
#if (UNITY_ENGINE || UNITY_5_3_OR_NEWER) && !NET_4_6 && !NET_STANDARD_2_0
                using (var gzipstream = new Unity.IO.Compression.GZipStream(memstream, Unity.IO.Compression.CompressionLevel.Optimal, false))
#else
                using (var gzipstream = new System.IO.Compression.GZipStream(memstream, System.IO.Compression.CompressionLevel.Optimal, false))
#endif
                {
                    gzipstream.Write(data, 0, data.Length);
                    gzipstream.Flush();
                    return memstream.ToArray();
                }
            }
            public static byte[] DecompressFunc_GZip(byte[] data, string token, ulong seq)
            {
#if (UNITY_ENGINE || UNITY_5_3_OR_NEWER) && !NET_4_6 && !NET_STANDARD_2_0
                using (var gzipstream = new Unity.IO.Compression.GZipStream(new MemoryStream(data), Unity.IO.Compression.CompressionMode.Decompress, false))
#else
                using (var gzipstream = new System.IO.Compression.GZipStream(new MemoryStream(data), System.IO.Compression.CompressionMode.Decompress, false))
#endif
                {
                    using (var decompressed = new MemoryStream())
                    {
                        gzipstream.CopyTo(decompressed);
                        return decompressed.ToArray();
                    }
                }
            }
        }

        static HttpRequestBase()
        {
#if UNITY_ENGINE || UNITY_5_3_OR_NEWER
            PreferredImplement = "unity";
#endif
            PreferredPrepareMethod = "default";
            RequestDataPrepareFuncs["default"] = PrepareFuncHelper.Prepare_Default;
            RequestDataPrepareFuncs["json"] = PrepareFuncHelper.Prepare_Json;
            RequestDataPrepareFuncs["form2json"] = PrepareFuncHelper.Prepare_Form2Json;

            CompressFuncs["gzip"] = PrepareFuncHelper.CompressFunc_GZip;
            DecompressFuncs["gzip"] = PrepareFuncHelper.DecompressFunc_GZip;
        }

        public static HttpRequestBase Create(string url, HttpRequestData headers, HttpRequestData data, string dest)
        {
            var req = HttpRequestCreator.Create(PreferredImplement, url, headers, data, dest);
            if (req == null)
            {
                req = new HttpRequestLegacy(url, headers, data, dest);
            }
            return req;
        }
    }
}

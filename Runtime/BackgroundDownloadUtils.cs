using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using Unity.Networking;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public static class BackgroundDownloadUtils
    {
#if !UNITY_EDITOR && (UNITY_IPHONE || UNITY_ANDROID || UNITY_WSA)
        public const bool IsValid = true;
#else
        public const bool IsValid = false;
#endif
        public static BackgroundDownload CreateBackgroundDownloadRequest(string url, string path)
        {
            var uri = new Uri(url);
            var downloads = BackgroundDownload.backgroundDownloads;
            for (int i = 0; i < downloads.Length; ++i)
            {
                var download = downloads[i];
                var config = download.config;
                if (config.url == uri)
                {
                    return download;
                }
            }

            return BackgroundDownload.Start(uri, path);
        }
        public static object CreateDownloadRequest(string url, string path)
        {
#if !UNITY_EDITOR && (UNITY_IPHONE || UNITY_ANDROID || UNITY_WSA)
            return CreateBackgroundDownloadRequest(url, path);
#else
            return HttpRequestUtils.CreateDownloadRequest(url, path);
#endif
        }

        private static char[] _PathSeparators = new char[] { '\\', '/' };
        public static System.Collections.IEnumerator DownloadBackgroundWork(TaskProgress prog, string url, string path, Action<string> onDone = null, Action<long> onReportProgress = null, Func<string, bool> checkFunc = null)
        {
            prog.Total = 100;
            prog.Length = 5;

            bool cancelled = false;
            bool done = false;

            try
            {
                while (true)
                {
                    var interPath = path + ".download";
                    var driverindex = interPath.IndexOf(":");
                    if (driverindex >= 0)
                    {
                        interPath = interPath.Substring(driverindex + 1);
                    }
                    interPath = interPath.TrimStart(_PathSeparators).ToLower();
                    var req = CreateBackgroundDownloadRequest(url, interPath);
                    prog.Task = req;
                    prog.OnCancel += () =>
                    {
                        req.Dispose();
                        cancelled = true;
                    };

                    var downloaded = req.progress;
                    var downloadtick = Environment.TickCount;
                    while (req.status == BackgroundDownloadStatus.Downloading)
                    {
                        yield return null;
                        var newtick = Environment.TickCount;
                        if (newtick - downloadtick > 1000)
                        {
                            var newdownloaded = req.progress;
                            if (newdownloaded > downloaded)
                            {
                                downloaded = newdownloaded;
                                downloadtick = newtick;
                                prog.Length = 5L + (long)(((float)newdownloaded) * 90f);
                                if (onReportProgress != null)
                                {
                                    onReportProgress(prog.Length);
                                }
                            }
                            //else // TODO: ¶ÏµãÐø´«
                            //{
                            //    if (newtick - downloadtick > 15000)
                            //    {
                            //        req.Dispose();
                            //        break;
                            //    }
                            //}
                        }
                    }
                    req.Dispose();

                    if (cancelled)
                    {
                        prog.Error = "canceled";
                        break;
                    }
                    if (req.status == BackgroundDownloadStatus.Failed)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                    }
                    else //if (req.status == BackgroundDownloadStatus.Done)
                    {
                        var realinterPath = req.config.filePath;
                        if (checkFunc == null || checkFunc(realinterPath))
                        {
                            if (realinterPath.Equals(interPath, StringComparison.InvariantCultureIgnoreCase))
                            {
                                PlatDependant.MoveFile(interPath, path);
                            }
                            else
                            {
                                PlatDependant.CopyFile(realinterPath, path);
                                PlatDependant.DeleteFile(interPath);
                            }
                            break;
                        }
                        else
                        {
                            PlatDependant.DeleteFile(realinterPath);
                            PlatDependant.DeleteFile(interPath);
                        }
                    }
                }

                done = true;
                if (prog.Error == null)
                {
                    prog.Length = 95;
                    if (onDone != null)
                    {
                        onDone(null);
                    }
                }
                else
                {
                    if (onDone != null)
                    {
                        onDone(prog.Error);
                    }
                }
            }
            finally
            {
                if (!done)
                {
                    if (prog.Error == null)
                    {
                        prog.Error = "coroutine not done correctly";
                    }
                }
                prog.Done = true;
            }
        }

        public static TaskProgress DownloadBackground(string url, string path, Action<string> onDone = null, Action<long> onReportProgress = null, Func<string, bool> checkFunc = null)
        {
            var prog = new TaskProgress();
            CoroutineRunner.StartCoroutine(DownloadBackgroundWork(prog, url, path, onDone, onReportProgress, checkFunc));
            return prog;
        }

        public static TaskProgress Download(string url, string path, Action<string> onDone = null, Action<long> onReportProgress = null, Func<string, bool> checkFunc = null)
        {
#if !UNITY_EDITOR && (UNITY_IPHONE || UNITY_ANDROID || UNITY_WSA)
            return DownloadBackground(url, path, onDone, onReportProgress, checkFunc);
#else
            return HttpRequestUtils.DownloadBackground(url, path, onDone, onReportProgress, checkFunc);
#endif
        }
    }
}
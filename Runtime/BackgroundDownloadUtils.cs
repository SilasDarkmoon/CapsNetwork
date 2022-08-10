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
                else if (config.filePath == path)
                {
                    download.Dispose();
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
        public static TaskProgress DownloadBackground(string url, string path, Action<string> onDone = null, Action<long> onReportProgress = null, Func<string, bool> checkFunc = null)
        {
            return PlatDependant.RunBackgroundLongTime(prog =>
            {
                prog.Total = 1000000L;
                prog.Length = 50000L;

                bool cancelled = false;
                bool done = false;

                try
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    UnityEngine.AndroidJNI.AttachCurrentThread();
#endif
                    while (true)
                    {
                        var interPath = path + ".download";
                        if (interPath.StartsWith(ThreadSafeValues.AppPersistentDataPath, StringComparison.InvariantCultureIgnoreCase))
                        {
                            interPath = interPath.Substring(ThreadSafeValues.AppPersistentDataPath.Length);
                        }
                        else
                        {
                            var driverindex = interPath.IndexOf(":");
                            if (driverindex >= 0)
                            {
                                interPath = interPath.Substring(driverindex + 1);
                            }
                        }
                        interPath = interPath.TrimStart(_PathSeparators).ToLower();
                        interPath = interPath.Replace('\\', '/');
                        var req = CreateBackgroundDownloadRequest(url, interPath);
                        prog.Task = req;
                        prog.OnCancel += () =>
                        {
                            req.Dispose();
                            cancelled = true;
                        };

                        var downloaded = req.progress;
                        while (req.status == BackgroundDownloadStatus.Downloading)
                        {
                            PlatDependant.Sleep(200);
                            var newdownloaded = req.progress;
                            if (newdownloaded > downloaded)
                            {
                                downloaded = newdownloaded;
                                prog.Length = 50000L + (long)(((float)newdownloaded) * 900000f);
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
                        req.Dispose();

                        if (cancelled)
                        {
                            prog.Error = "canceled";
                            break;
                        }
                        if (req.status == BackgroundDownloadStatus.Failed)
                        {
                            PlatDependant.Sleep(500);
                        }
                        else //if (req.status == BackgroundDownloadStatus.Done)
                        {
                            var realinterPath = req.config.filePath;
                            var fullreal = System.IO.Path.Combine(ThreadSafeValues.AppPersistentDataPath, realinterPath);
                            var fullinter = System.IO.Path.Combine(ThreadSafeValues.AppPersistentDataPath, interPath);
                            if (checkFunc == null || checkFunc(fullreal))
                            {
                                if (realinterPath.Equals(interPath, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    PlatDependant.MoveFile(fullinter, path);
                                }
                                else
                                {
                                    PlatDependant.CopyFile(fullreal, path);
                                    PlatDependant.DeleteFile(fullinter);
                                }
                                break;
                            }
                            else
                            {
                                PlatDependant.DeleteFile(fullreal);
                                PlatDependant.DeleteFile(fullinter);
                            }
                        }
                    }

                    done = true;
                    if (prog.Error == null)
                    {
                        prog.Length = 950000L;
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
                            prog.Error = "Background downloading is not done correctly.";
                        }
                    }
                    prog.Done = true;
                }
            });
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
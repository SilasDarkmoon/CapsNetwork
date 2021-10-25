using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using Capstones.UnityEngineEx;

namespace Capstones.Net
{
    public static class HttpRequestUtils
    {
        public static HttpRequestBase CreateDownloadRequest(string url, string path, Func<string, bool> checkFunc = null)
        {
            PlatDependant.DeleteFile(path);
            var downloadingPath = path + ".downloading";
            var downloadingInfoPath = downloadingPath + ".info";
            bool rangeEnabled = false;
            if (PlatDependant.IsFileExist(downloadingPath) && PlatDependant.IsFileExist(downloadingInfoPath))
            {
                var lines = PlatDependant.ReadAllLines(downloadingInfoPath);
                if (lines != null && lines.Length > 0)
                {
                    var oldurl = lines[0];
                    if (oldurl == url)
                    {
                        rangeEnabled = true;
                    }
                }
            }
            if (!rangeEnabled)
            {
                PlatDependant.DeleteFile(downloadingPath);
                PlatDependant.WriteAllText(downloadingInfoPath, url);
            }

            var req = new HttpRequestLegacy(url, null, null, downloadingPath); // UnityWebRequest is supposed to be used in main thread.
            req.RangeEnabled = rangeEnabled;
            req.OnDone = () =>
            {
                if (req.Error == null)
                {
                    if (checkFunc == null || checkFunc(downloadingPath))
                    {
                        PlatDependant.MoveFile(downloadingPath, path);
                        PlatDependant.DeleteFile(downloadingInfoPath);
                    }
                    else
                    {
                        PlatDependant.DeleteFile(downloadingPath);
                        PlatDependant.DeleteFile(downloadingInfoPath);
                    }
                }
            };
            return req;
        }

        public static TaskProgress DownloadBackground(string url, string path, Action<string> onDone = null, Action<long> onReportProgress = null, Func<string, bool> checkFunc = null)
        {
            return PlatDependant.RunBackgroundLongTime(prog =>
            {
                prog.Total = 100;
                prog.Length = 5;

                bool cancelled = false;

                while (true)
                {
                    var req = CreateDownloadRequest(url, path, checkFunc);
                    prog.Task = req;
                    prog.OnCancel += () =>
                    {
                        req.StopRequest();
                        cancelled = true;
                    };
                    System.Threading.ManualResetEvent waitHandle = new System.Threading.ManualResetEvent(false);
                    req.OnDone += () =>
                    {
                        waitHandle.Set();
                    };
                    req.StartRequest();

                    var downloaded = req.Length;
                    var downloadtick = Environment.TickCount;
                    while (!waitHandle.WaitOne(1000))
                    {
                        var newdownloaded = req.Length;
                        var newtick = Environment.TickCount;
                        if (newdownloaded > downloaded)
                        {
                            downloaded = newdownloaded;
                            downloadtick = newtick;
                            if (req.Total > 0)
                            {
                                prog.Length = 5L + (long)(((float)(newdownloaded)) / ((float)req.Total) * 90f);
                                if (onReportProgress != null)
                                {
                                    onReportProgress(prog.Length);
                                }
                            }
                        }
                        else
                        {
                            if (newtick - downloadtick > 15000)
                            {
                                req.StopRequest();
                                break;
                            }
                        }
                    }
                    waitHandle.Dispose();

                    if (cancelled)
                    {
                        prog.Error = "canceled";
                        break;
                    }
                    if (req.Error == null && PlatDependant.IsFileExist(path))
                    {
                        break;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }

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
            });
        }
    }
}
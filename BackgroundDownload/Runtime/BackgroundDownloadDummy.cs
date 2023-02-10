using System.Collections.Generic;

namespace Unity.Networking
{
    class BackgroundDownloadDummy : BackgroundDownload
    {
        public BackgroundDownloadDummy(BackgroundDownloadConfig config)
            : base(config)
        {
            _status = BackgroundDownloadStatus.Failed;
            _error = "Not implemented.";
        }

        public override bool keepWaiting { get { return false; } }

        protected override float GetProgress() { return 1.0f; }

        internal static Dictionary<string, BackgroundDownload> LoadDownloads()
        {
            return new Dictionary<string, BackgroundDownload>();
        }

        internal static void SaveDownloads(Dictionary<string, BackgroundDownload> downloads)
        {
        }
    }
}

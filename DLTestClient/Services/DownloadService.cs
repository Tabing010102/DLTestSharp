using DLTestClient.Utils;
using Downloader;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DLTestClient.Services
{
    public class DownloadService
    {
        private int _maxConcurrentDownloads = 2;
        private int _maxThreadsPerDownload = 2;
        private string _cacheDir = "./cache";

        private ILogger _logger;
        public DownloadService(ILogger<DownloadService> logger)
        {
            _logger = logger;
        }

        public List<string> DownloadUrls { get; set; } = new();
        private int _downloadUrlsPos = 0;

        public int DownloadingCount { get; set; } = 0;
        public int DownloadedCount { get; set; } = 0;
        public Dictionary<int, (long, long, double)> DownloaderStatDict { get; } = new();

        public void StartDownload(CancellationToken cts = default)
        {
            // init
            var downloaders = new List<Downloader.DownloadService>();
            var downloadConfig = new DownloadConfiguration
            {
                ChunkCount = _maxThreadsPerDownload,
                ParallelDownload = true,
            };
            for (int i = 0; i < _maxConcurrentDownloads; i++)
            {
                var downloader = new Downloader.DownloadService(downloadConfig);
                downloader.DownloadStarted += Downloader_DownloadStarted;
                downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
                downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
                DownloaderStatDict.Add(downloader.GetHashCode(), (0, 0, 0.0));
                downloaders.Add(downloader);
            }
            // download
            var downloadTasks = new List<Task>();
            foreach (var downloader in downloaders)
            {
                var dirInfo = new DirectoryInfo(_cacheDir);
                downloadTasks.Add(Task.Run(async () =>
                {
                    while (true)
                    {
                        var downloadUrl = GetNextDownloadUrl();
                        if (downloadUrl == null)
                        {
                            _logger.LogInformation($"[#{downloader.GetHashCode()}] No more url to download");
                            break;
                        }
                        _logger.LogInformation($"[#{downloader.GetHashCode()}] Downloading next url: {downloadUrl}");
                        await downloader.DownloadFileTaskAsync(downloadUrl, dirInfo, cts);
                    }
                }, cts));
            }
            Task.WaitAll(downloadTasks.ToArray(), cts);
            // dispose
            foreach (var downloader in downloaders)
            {
                _logger.LogInformation($"Disposing [#{downloader.GetHashCode()}]");
                downloader.Dispose();
            }
        }

        private void Downloader_DownloadStarted(object? sender, DownloadStartedEventArgs e)
        {
            if (sender == null) { return; }
            if (sender is Downloader.DownloadService ds)
            {
                DownloadingCount++;
                _logger.LogInformation($"[#{ds.GetHashCode()}] Download \"{e.FileName}\" started, " +
                    $"total {HumanizeUtil.BytesToString(e.TotalBytesToReceive)}");
            }
        }

        private void Downloader_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if (sender == null) { return; }
            if (sender is Downloader.DownloadService ds)
            {
                DownloadingCount--;
                DownloadedCount++;
            }
        }

        private void Downloader_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
        {
            if (sender == null) { return; }
            if (sender is Downloader.DownloadService ds)
            {
                (long, long, double) stat;
                stat.Item1 = e.ReceivedBytesSize;
                stat.Item2 = e.TotalBytesToReceive;
                stat.Item3 = e.BytesPerSecondSpeed;
                DownloaderStatDict[ds.GetHashCode()] = stat;
            }
        }

        private string? GetNextDownloadUrl()
        {
            if (_downloadUrlsPos < DownloadUrls.Count)
            {
                return DownloadUrls[_downloadUrlsPos++];
            }
            else
            {
                return null;
            }
        }
    }
}

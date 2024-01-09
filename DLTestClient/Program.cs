using DLTestClient.Services;
using DLTestClient.Utils;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DLTestClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole((options) =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                    options.IncludeScopes = true;
                });
            });
            var _logger = loggerFactory.CreateLogger<Program>();
            var downloadService = new DownloadService(loggerFactory.CreateLogger<DownloadService>());

            var channel = GrpcChannel.ForAddress("http://localhost:5027");
            var dlTestClient = new DLTest.DLTest.DLTestClient(channel);
            var dlResponse = dlTestClient.GetDLFiles(new DLTest.GetDLRequest
            {
                Id = 0
            });

            downloadService.DownloadUrls = dlResponse.File.Chunks.Select(c => c.PublicUrl).ToList();
            var task = Task.Run(async () =>
            {
                while (true)
                {
                    var msg = "";
                    bool first = true;
                    foreach (var stat in downloadService.DownloaderStatDict)
                    {
                        if (!first) { msg += "\n      "; }
                        first = false;
                        msg += $"[#{stat.Key}]: {HumanizeUtil.BytesToString(stat.Value.Item1)} / " +
                        $"{HumanizeUtil.BytesToString(stat.Value.Item2)}, " +
                        $"speed = {HumanizeUtil.BytesToString(stat.Value.Item3):0.00}/s"; ;
                    }
                    _logger.LogInformation($"{msg}");
                    await Task.Delay(1000);
                }
            });
            downloadService.StartDownload();
            task.Wait();
        }
    }
}

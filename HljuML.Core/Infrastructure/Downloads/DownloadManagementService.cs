using System.Net;
using System.Threading.Channels;
using ColorMC.Core;
using HljuML.Shared.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.Infrastructure.Downloads;

public sealed partial class DownloadManagementService(
    ILogger<DownloadManagementService> logger,
    DownloadTrafficService traffic) : BackgroundService
{
    private const int WorkerCount = 128;
    public int DownloadConcurrency => traffic.Concurrency;
    private static readonly TimeSpan TransferIdleTimeout = TimeSpan.FromSeconds(60);

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        UseCookies = false
    });

    private readonly object _sync = new();
    private readonly Dictionary<Guid, DownloadJob> _jobs = [];
    private readonly HashSet<string> _reservedPaths = new(PathComparer);
    private readonly Channel<QueuedDownload> _queue = Channel.CreateUnbounded<QueuedDownload>();

    public Guid Enqueue(DownloadRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        if (request.Files.Count == 0)
        {
            throw new ArgumentException("下载请求至少需要一个文件。", nameof(request));
        }

        var job = new DownloadJob(request.Name);
        var files = new List<QueuedFile>(request.Files.Count);
        var paths = new HashSet<string>(PathComparer);
        foreach (var file in request.Files)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(file.Name);
            if (!file.Source.IsAbsoluteUri || file.Source.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException("下载地址必须是 HTTP 或 HTTPS 地址。", nameof(request));
            }

            if (!Path.IsPathFullyQualified(file.DestinationPath))
            {
                throw new ArgumentException("下载目标必须使用绝对文件路径。", nameof(request));
            }

            var path = Path.GetFullPath(file.DestinationPath);
            if (string.IsNullOrEmpty(Path.GetFileName(path)))
            {
                throw new ArgumentException("下载目标必须包含文件名。", nameof(request));
            }

            if (!paths.Add(path))
            {
                throw new ArgumentException("请求中包含重复的下载目标。", nameof(request));
            }

            var item = new DownloadItemSnapshot(Guid.NewGuid(), file.Name, path);
            job.Items.Add(item.Id, item);
            files.Add(new QueuedFile(file.Source, item));
        }

        job.TotalFiles = files.Count;
        lock (_sync)
        {
            if (paths.Overlaps(_reservedPaths))
            {
                throw new InvalidOperationException("已有下载请求使用同一个目标路径。");
            }

            if (!_queue.Writer.TryWrite(new QueuedDownload(job, files)))
            {
                throw new InvalidOperationException("下载服务已停止接收请求。");
            }

            _jobs.Add(job.Id, job);
            _reservedPaths.UnionWith(paths);
        }

        NotifyTaskCreated(job.Id);
        return job.Id;
    }

    public DownloadSnapshot? GetDownload(Guid id)
    {
        lock (_sync)
        {
            return _jobs.TryGetValue(id, out var job) ? job.Snapshot() : null;
        }
    }

    public IReadOnlyList<DownloadSnapshot> GetActiveDownloads()
    {
        lock (_sync)
        {
            return _jobs.Values.Where(job => job.IsActive).Select(job => job.Snapshot()).ToArray();
        }
    }

    public int GetActiveDownloadCount()
    {
        lock (_sync)
        {
            return _jobs.Values.Count(job => job.IsActive);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(Enumerable.Range(0, WorkerCount).Select(_ => RunWorkerAsync(stoppingToken)));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _queue.Writer.TryComplete();
        }

        try
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ColorMCCore.Error -= LogColorMcError;
        }
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var job = request.Job;
            lock (_sync)
            {
                job.Status = DownloadStatus.Running;
            }

            foreach (var file in request.Files)
                await DownloadFileAsync(job, file, stoppingToken).ConfigureAwait(false);

            lock (_sync)
            {
                foreach (var file in request.Files)
                    _reservedPaths.Remove(file.Item.DestinationPath);

                if (job.Items.Values.Any(item => item.Status == DownloadItemStatus.Canceled))
                {
                    FinishJob(job, DownloadStatus.Canceled, "下载已取消。");
                }
                else if (job.Items.Values.Any(item => item.Status == DownloadItemStatus.Failed))
                {
                    FinishJob(job, DownloadStatus.Failed, "部分文件下载失败。");
                }
                else
                {
                    FinishJob(job, DownloadStatus.Succeeded);
                }
            }
        }
    }

    private async Task DownloadFileAsync(DownloadJob job, QueuedFile file, CancellationToken stoppingToken)
    {
        var item = file.Item;
        var path = item.DestinationPath;
        var directory = Path.GetDirectoryName(path)!;
        var temporaryPath = Path.Combine(directory, $".hljuml-{item.Id:N}.part");
        var ownsTemporaryFile = false;

        void Report(DownloadItemSnapshot update)
        {
            item = update;
            lock (_sync)
            {
                job.Items[item.Id] = item;
            }
        }

        try
        {
            using var slot = await traffic.AcquireAsync(stoppingToken).ConfigureAwait(false);
            stoppingToken.ThrowIfCancellationRequested();
            Report(item with { Status = DownloadItemStatus.Downloading });
            if (File.Exists(path) || Directory.Exists(path))
            {
                logger.LogWarning("下载目标已存在：{FileName}；来源 {Source}；目标 {Destination}",
                    item.Name, GetLogSource(file.Source.AbsoluteUri), path);
                Report(item with { Status = DownloadItemStatus.Failed, Error = "下载目标已存在。" });
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TransferIdleTimeout);
            var token = timeout.Token;
            using var response = await Client.GetAsync(file.Source, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                throw new IOException("Unexpected partial response for a full download.");
            }

            Report(item with { TotalBytes = response.Content.Headers.ContentLength });
            Directory.CreateDirectory(directory);
            await using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 64 * 1024, true))
            {
                ownsTemporaryFile = true;
                await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var buffer = new byte[64 * 1024];
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                    await traffic.ThrottleAsync(bytesRead, stoppingToken).ConfigureAwait(false);
                    timeout.CancelAfter(TransferIdleTimeout);
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token).ConfigureAwait(false);
                    Report(item with { DownloadedBytes = item.DownloadedBytes + bytesRead });
                }

                if (item.TotalBytes is { } expected && item.DownloadedBytes != expected)
                {
                    throw new IOException("The downloaded length did not match the response length.");
                }

                await destination.FlushAsync(token).ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path);
            ownsTemporaryFile = false;
            Report(item with { Status = DownloadItemStatus.Succeeded });
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Report(item with { Status = DownloadItemStatus.Canceled });
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException
                                          or OperationCanceledException or ArgumentException or NotSupportedException)
        {
            logger.LogError(error,
                "下载失败：{DownloadId}/{DownloadItemId}；文件 {FileName}；来源 {Source}；目标 {Destination}；已下载 {DownloadedBytes}/{TotalBytes} 字节",
                job.Id, item.Id, item.Name, GetLogSource(file.Source.AbsoluteUri), path,
                item.DownloadedBytes, item.TotalBytes);
            var message = error switch
            {
                HttpRequestException { StatusCode: { } code } => $"服务器返回 HTTP {(int)code}。",
                OperationCanceledException => "等待服务器响应超时。",
                _ => "无法下载或保存文件。"
            };
            Report(item with { Status = DownloadItemStatus.Failed, Error = message });
        }
        finally
        {
            if (ownsTemporaryFile)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(error, "Failed to remove partial download {DownloadItemId}", item.Id);
                }
            }
        }
    }

    private void FinishJob(DownloadJob job, DownloadStatus status, string? error = null)
    {
        if (!job.IsActive)
        {
            return;
        }

        job.Status = status;
        job.Error = error;
        if (status == DownloadStatus.Succeeded)
        {
            job.Summary = job.Snapshot() with { Items = Array.Empty<DownloadItemSnapshot>() };
            job.Items.Clear();
        }

        job.Batches.Clear();
    }

    private sealed record QueuedDownload(DownloadJob Job, IReadOnlyList<QueuedFile> Files);

    private sealed record QueuedFile(Uri Source, DownloadItemSnapshot Item);

    private sealed class DownloadJob(string name, Guid? instanceId = null)
    {
        internal Guid Id { get; } = Guid.NewGuid();
        internal DownloadStatus Status { get; set; }
        internal bool IsActive => Status is DownloadStatus.Queued or DownloadStatus.Running;
        internal int? TotalFiles { get; set; }
        internal string? Error { get; set; }
        internal string? Phase { get; set; }
        internal bool IsOperation { get; init; }
        internal List<ColorMcBatch> Batches { get; } = [];
        internal DownloadSnapshot? Summary { get; set; }
        internal Dictionary<Guid, DownloadItemSnapshot> Items { get; } = [];

        internal DownloadSnapshot Snapshot()
        {
            if (Summary is not null)
            {
                return Summary;
            }

            var items = Items.Values.ToArray();
            var totalBytes = items.Length == TotalFiles && items.All(item => item.TotalBytes.HasValue)
                ? items.Sum(item => item.TotalBytes)
                : null;
            return new DownloadSnapshot(Id, name, instanceId, Status, TotalFiles,
                    items.Count(item => item.Status == DownloadItemStatus.Succeeded),
                    items.Count(item => item.Status == DownloadItemStatus.Failed),
                    items.Sum(item => item.DownloadedBytes), totalBytes, Array.AsReadOnly(items), Error)
                { Phase = Phase };
        }
    }
}
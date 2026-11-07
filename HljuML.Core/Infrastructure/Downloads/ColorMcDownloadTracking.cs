using System.Diagnostics;
using ColorMC.Core;
using ColorMC.Core.GuiHandle;
using ColorMC.Core.Objs;
using HljuML.Core.GamePlay;
using HljuML.Shared.Model;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.Infrastructure.Downloads;

public sealed partial class DownloadManagementService
{
    internal async Task<T> TrackGameDownloadsAsync<T>(Guid instanceId, string instanceName, Func<Task<T>> launch,
        IProgress<GameLaunchProgress>? progress, CancellationToken cancellationToken)
    {
        // ColorMC requests its download handle in the caller's execution context, before dispatching to workers.
        var inLaunchContext = new AsyncLocal<bool> { Value = true };
        var batches = new List<DownloadJob>();
        var closed = false;

        void Attach(DownloadEventArgs args)
        {
            if (!inLaunchContext.Value || args.GuiHandle is not null)
                return;

            lock (_sync)
            {
                if (closed)
                    return;

                var job = new DownloadJob(instanceName, instanceId) { Status = DownloadStatus.Running };
                batches.Add(job);
                _jobs.Add(job.Id, job);
                args.GuiHandle = new ColorMcBatch(this, job, progress);
            }
        }

        ColorMCCore.Download += Attach;
        try
        {
            return await launch().ConfigureAwait(false);
        }
        finally
        {
            ColorMCCore.Download -= Attach;
            lock (_sync)
            {
                closed = true;
                foreach (var job in batches.Where(batch => batch.IsActive))
                    CloseColorMcBatch(job, cancellationToken.IsCancellationRequested);
            }
        }
    }

    private void CloseColorMcBatch(DownloadJob job, bool canceled)
    {
        var itemStatus = canceled ? DownloadItemStatus.Canceled : DownloadItemStatus.Failed;
        var message = canceled ? "下载已取消。" : "ColorMC 未确认该批次的全部文件完成。";
        foreach (var item in job.Items.Values.Where(item => item.Status != DownloadItemStatus.Succeeded).ToArray())
            job.Items[item.Id] = item with { Status = itemStatus, Error = message };

        FinishJob(job, canceled ? DownloadStatus.Canceled : DownloadStatus.Failed, message);
    }

    private sealed class ColorMcBatch(DownloadManagementService owner, DownloadJob job,
        IProgress<GameLaunchProgress>? progress) : IDownloadGui
    {
        private readonly Dictionary<FileItemObj, Guid> _itemIds = new(ReferenceEqualityComparer.Instance);
        private int _succeeded;
        private long _lastReport;

        public void Update(int thread, bool state, int count) { }

        public void UpdateTask(UpdateType type, int data)
        {
            // ItemDone counts failures and may repeat for cached files. Count distinct Done items instead.
            if (type != UpdateType.AddItems)
                return;

            GameLaunchProgress? update;
            lock (owner._sync)
            {
                if (!job.IsActive)
                    return;

                job.TotalFiles = data;
                FinishIfComplete();
                update = CaptureProgress(force: true);
            }

            Report(update);
        }

        public void UpdateItem(int thread, FileItemObj file)
        {
            GameLaunchProgress? update;
            lock (owner._sync)
            {
                if (!job.IsActive)
                    return;
                if (!_itemIds.TryGetValue(file, out var id))
                    _itemIds.Add(file, id = Guid.NewGuid());
                else if (job.Items[id].Status == DownloadItemStatus.Succeeded)
                    return;

                var status = file.State switch
                {
                    DownloadItemState.GetInfo or DownloadItemState.Download => DownloadItemStatus.Downloading,
                    DownloadItemState.Action => DownloadItemStatus.Processing,
                    DownloadItemState.Pause => DownloadItemStatus.Paused,
                    DownloadItemState.Error => DownloadItemStatus.Retrying,
                    DownloadItemState.Done => DownloadItemStatus.Succeeded,
                    _ => DownloadItemStatus.Queued
                };
                var item = new DownloadItemSnapshot(id, file.Name, file.Local, status, file.NowSize,
                    file.AllSize > 0 ? file.AllSize : null);
                job.Items[id] = item;
                if (status == DownloadItemStatus.Succeeded)
                    _succeeded++;

                FinishIfComplete();
                update = CaptureProgress(force: !job.IsActive, item);
            }

            Report(update);
        }

        private void FinishIfComplete()
        {
            if (job.TotalFiles is > 0 && _succeeded == job.TotalFiles)
                owner.FinishJob(job, DownloadStatus.Succeeded);
        }

        private GameLaunchProgress? CaptureProgress(bool force, DownloadItemSnapshot? item = null)
        {
            if (progress is null || (!force && Stopwatch.GetElapsedTime(_lastReport) < TimeSpan.FromMilliseconds(150)))
                return null;

            _lastReport = Stopwatch.GetTimestamp();
            var operation = item?.Status switch
            {
                DownloadItemStatus.Processing => "正在处理下载文件",
                DownloadItemStatus.Retrying => "正在重试下载",
                _ => "正在下载"
            };
            return new(operation, _succeeded, job.TotalFiles, item?.Name, item?.DownloadedBytes ?? 0, item?.TotalBytes);
        }

        private void Report(GameLaunchProgress? update)
        {
            if (update is null)
                return;

            try
            {
                progress?.Report(update);
            }
            catch (Exception error)
            {
                owner.LogProgressError(error, job.Id);
            }
        }
    }

    private void LogProgressError(Exception error, Guid id) =>
        logger.LogWarning(error, "Failed to report launch download progress for {DownloadId}", id);
}

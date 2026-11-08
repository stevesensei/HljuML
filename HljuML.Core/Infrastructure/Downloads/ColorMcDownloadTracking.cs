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
        DownloadJob? job = null;

        DownloadJob GetJob()
        {
            bool created;
            DownloadJob current;
            lock (_sync)
            {
                created = job is null;
                if (job is null)
                {
                    job = new DownloadJob(instanceName, instanceId)
                    {
                        IsOperation = true, Status = DownloadStatus.Running, Phase = "正在准备启动文件"
                    };
                    _jobs.Add(job.Id, job);
                }

                current = job;
            }

            if (created)
            {
                NotifyTaskCreated(current.Id);
            }

            return current;
        }

        try
        {
            var result = await TrackColorMcAsync(GetJob, launch, progress, cancellationToken).ConfigureAwait(false);
            if (job is not null)
            {
                CompleteOperation(job.Id, result is null || result is false
                        ? DownloadStatus.Failed
                        : DownloadStatus.Succeeded,
                    result is null || result is false ? "游戏进程未启动。" : null);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            if (job is not null)
            {
                CompleteOperation(job.Id, DownloadStatus.Canceled, "启动已取消。");
            }

            throw;
        }
        catch (Exception error)
        {
            if (job is not null)
            {
                CompleteOperation(job.Id, DownloadStatus.Failed, error.Message);
            }

            throw;
        }
    }

    private async Task<T> TrackColorMcAsync<T>(Func<DownloadJob> getJob, Func<Task<T>> action,
        IProgress<GameLaunchProgress>? progress, CancellationToken cancellationToken)
    {
        // ColorMC obtains its handle before dispatching workers, so only the initiating context attaches batches.
        var inContext = new AsyncLocal<bool> { Value = true };
        var batches = new List<ColorMcBatch>();
        var closed = false;

        void Attach(DownloadEventArgs args)
        {
            if (!inContext.Value || args.GuiHandle is not null || closed)
            {
                return;
            }

            var job = getJob();
            lock (_sync)
            {
                if (closed || !job.IsActive)
                {
                    return;
                }

                var batch = new ColorMcBatch(this, job, progress);
                batches.Add(batch);
                job.Batches.Add(batch);
                job.TotalFiles = null;
                args.GuiHandle = batch;
            }
        }

        ColorMCCore.Download += Attach;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            inContext.Value = false;
            ColorMCCore.Download -= Attach;
            lock (_sync)
            {
                closed = true;
                foreach (var batch in batches)
                    batch.Close(cancellationToken.IsCancellationRequested);
            }
        }
    }

    private sealed class ColorMcBatch(
        DownloadManagementService owner,
        DownloadJob job,
        IProgress<GameLaunchProgress>? progress) : IDownloadGui
    {
        private readonly Dictionary<FileItemObj, Guid> _itemIds = new(ReferenceEqualityComparer.Instance);
        private int? _totalFiles;
        private bool _closed;
        private long _lastReport;

        public void Update(int thread, bool state, int count)
        {
            if (!state)
            {
                lock (owner._sync)
                {
                    Close(false);
                }
            }
        }

        public void UpdateTask(UpdateType type, int data)
        {
            // AddItems is an absolute count; ItemDone repeats for cached files and also counts failures.
            if (type != UpdateType.AddItems)
            {
                return;
            }

            GameLaunchProgress? update;
            lock (owner._sync)
            {
                if (_closed || !job.IsActive)
                {
                    return;
                }

                _totalFiles = Math.Max(0, data);
                job.TotalFiles = job.Batches.All(batch => batch._totalFiles.HasValue)
                    ? job.Batches.Sum(batch => batch._totalFiles!.Value)
                    : null;
                CloseIfComplete();
                update = CaptureProgress(true);
            }

            Report(update);
        }

        public void UpdateItem(int thread, FileItemObj file)
        {
            GameLaunchProgress? update;
            lock (owner._sync)
            {
                if (_closed || !job.IsActive)
                {
                    return;
                }

                if (!_itemIds.TryGetValue(file, out var id))
                {
                    _itemIds.Add(file, id = Guid.NewGuid());
                }
                else if (job.Items[id].Status == DownloadItemStatus.Succeeded)
                {
                    return;
                }

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
                CloseIfComplete();
                update = CaptureProgress(_closed, item);
            }

            Report(update);
        }

        private void CloseIfComplete()
        {
            if (_totalFiles is { } total && _itemIds.Count == total &&
                _itemIds.Values.All(id => job.Items[id].Status == DownloadItemStatus.Succeeded))
            {
                _closed = true;
            }
        }

        internal void Close(bool canceled)
        {
            if (_closed || !job.IsActive)
            {
                return;
            }

            _closed = true;
            var message = canceled ? "下载已取消。" : "ColorMC 未确认该批次的全部文件完成。";
            foreach (var id in _itemIds.Values)
            {
                var item = job.Items[id];
                if (item.Status != DownloadItemStatus.Succeeded)
                {
                    job.Items[id] = item with
                    {
                        Status = canceled ? DownloadItemStatus.Canceled : DownloadItemStatus.Failed,
                        Error = message
                    };
                }
            }

            job.Error ??= message;
        }

        private GameLaunchProgress? CaptureProgress(bool force, DownloadItemSnapshot? item = null)
        {
            if (progress is null || (!force && Stopwatch.GetElapsedTime(_lastReport) < TimeSpan.FromMilliseconds(150)))
            {
                return null;
            }

            _lastReport = Stopwatch.GetTimestamp();
            return new GameLaunchProgress(job.Phase ?? "正在下载",
                job.Items.Values.Count(file => file.Status == DownloadItemStatus.Succeeded),
                job.TotalFiles, item?.Name, item?.DownloadedBytes ?? 0, item?.TotalBytes);
        }

        private void Report(GameLaunchProgress? update)
        {
            if (update is null)
            {
                return;
            }

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

    private void LogProgressError(Exception error, Guid id)
    {
        logger.LogWarning(error, "Failed to report download progress for {DownloadId}", id);
    }
}
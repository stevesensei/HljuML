using HljuML.Shared.Model;

namespace HljuML.Core.Infrastructure.Downloads;

public sealed partial class DownloadManagementService
{
    public event Action<Guid>? TaskCreated;

    public Guid CreateOperation(string name, Guid? instanceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var job = new DownloadJob(name, instanceId) { IsOperation = true };
        lock (_sync)
            _jobs.Add(job.Id, job);
        NotifyTaskCreated(job.Id);
        return job.Id;
    }

    public void SetOperationPhase(Guid id, string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        lock (_sync)
        {
            var job = GetOperation(id);
            if (job.IsActive)
                job.Phase = phase;
        }
    }

    public void CompleteOperation(Guid id, DownloadStatus status, string? error = null)
    {
        if (status is not (DownloadStatus.Succeeded or DownloadStatus.Failed or DownloadStatus.Canceled))
            throw new ArgumentOutOfRangeException(nameof(status), "操作必须以终态结束。");
        lock (_sync)
        {
            var job = GetOperation(id);
            if (!job.IsActive)
                return;
            foreach (var batch in job.Batches)
                batch.Close(status == DownloadStatus.Canceled);
            // A caller cannot turn an unconfirmed or failed child batch into a successful installation.
            if (status == DownloadStatus.Succeeded && job.Error is not null)
                status = DownloadStatus.Failed;
            FinishJob(job, status, error ?? job.Error ?? (status switch
            {
                DownloadStatus.Failed => "操作失败。",
                DownloadStatus.Canceled => "操作已取消。",
                _ => null
            }));
        }
    }

    public Task<T> TrackOperationAsync<T>(Guid operationId, Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        DownloadJob job;
        lock (_sync)
        {
            job = GetOperation(operationId);
            if (!job.IsActive)
                throw new InvalidOperationException("操作已结束。");
            job.Status = DownloadStatus.Running;
        }
        return TrackColorMcAsync(() => job, action, null, cancellationToken);
    }

    public IReadOnlyList<DownloadSnapshot> GetDownloads()
    {
        lock (_sync)
            return _jobs.Values.Select(job => job.Snapshot()).ToArray();
    }

    public bool HasPendingOrFailedDownloads()
    {
        lock (_sync)
            return _jobs.Values.Any(job => job.Status != DownloadStatus.Succeeded);
    }

    public (int TotalCount, int ActiveCount, bool HasPendingOrFailed) GetQueueSummary()
    {
        lock (_sync)
            return (_jobs.Count, _jobs.Values.Count(job => job.IsActive),
                _jobs.Values.Any(job => job.Status != DownloadStatus.Succeeded));
    }

    private DownloadJob GetOperation(Guid id)
    {
        if (!_jobs.TryGetValue(id, out var job) || !job.IsOperation)
            throw new KeyNotFoundException("找不到下载操作。");
        return job;
    }

    private void NotifyTaskCreated(Guid id)
    {
        if (TaskCreated is not { } handlers)
            return;
        foreach (Action<Guid> handler in handlers.GetInvocationList())
        {
            try { handler(id); }
            catch (Exception error) { LogProgressError(error, id); }
        }
    }
}

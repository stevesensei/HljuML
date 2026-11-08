using HljuML.Shared.Model;

namespace HljuML.Core.Infrastructure.Downloads;

public sealed partial class DownloadManagementService
{
    /// <summary>
    /// 将整合包文件归入既有安装任务。多个地址按顺序尝试，使用同一条目记录最终结果，
    /// 避免一次备用地址重试失败就永久污染父任务状态。
    /// </summary>
    internal async Task DownloadOperationFileAsync(Guid operationId, string name, string destination,
        IReadOnlyList<Uri> sources, CancellationToken token)
    {
        DownloadJob job;
        var item = new DownloadItemSnapshot(Guid.NewGuid(), name, destination);
        lock (_sync)
        {
            job = GetOperation(operationId);
            if (!job.IsActive) throw new InvalidOperationException("安装任务已结束。");
            job.Items.Add(item.Id, item);
            job.TotalFiles = (job.TotalFiles ?? 0) + 1;
        }
        foreach (var source in sources)
        {
            await DownloadFileAsync(job, new QueuedFile(source, item), token);
            token.ThrowIfCancellationRequested();
            lock (_sync)
                if (job.Items[item.Id].Status == DownloadItemStatus.Succeeded) return;
        }
        throw new IOException($"整合包文件下载失败，所有可用地址均已尝试：{name}");
    }
}

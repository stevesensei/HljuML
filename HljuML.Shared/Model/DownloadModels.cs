namespace HljuML.Shared.Model;

public sealed record DownloadFileRequest(string Name, Uri Source, string DestinationPath);

public sealed record DownloadRequest(string Name, IReadOnlyList<DownloadFileRequest> Files);

public enum DownloadStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public enum DownloadItemStatus
{
    Queued,
    Downloading,
    Processing,
    Paused,
    Retrying,
    Succeeded,
    Failed,
    Canceled
}

public sealed record DownloadItemSnapshot(Guid Id, string Name, string DestinationPath,
    DownloadItemStatus Status = DownloadItemStatus.Queued, long DownloadedBytes = 0, long? TotalBytes = null,
    string? Error = null);

public sealed record DownloadSnapshot(Guid Id, string Name, Guid? InstanceId, DownloadStatus Status,
    int? TotalFiles, int SucceededFiles, int FailedFiles, long DownloadedBytes, long? TotalBytes,
    IReadOnlyList<DownloadItemSnapshot> Items, string? Error)
{
    public string? Phase { get; init; }

    public double? Percent => TotalBytes switch
    {
        > 0 => Math.Min(100, DownloadedBytes * 100.0 / TotalBytes.Value),
        0 when Status == DownloadStatus.Succeeded => 100,
        _ => null
    };
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using HljuML.Core.Infrastructure.Downloads;
using HljuML.Shared.Model;

namespace HljuML.App.Core;

public sealed class DownloadQueueViewModel(DownloadManagementService downloads) : INotifyPropertyChanged
{
    private readonly Dictionary<Guid, DownloadTaskViewModel> _tasksById = new();
    private int _activeCount;
    private int _totalCount;

    public ObservableCollection<DownloadTaskViewModel> Tasks { get; } = new();
    public bool HasActiveDownloads => _activeCount > 0;
    public bool HasTasks => _totalCount > 0;
    public bool HasPendingOrFailedTasks { get; private set; }

    public string Summary => HasActiveDownloads ? $"当前有 {_activeCount} 项下载任务"
        : HasPendingOrFailedTasks ? "有下载未完成，请查看任务结果。"
        : HasTasks ? $"本次运行已完成 {_totalCount} 项下载。" : "当前没有下载任务。";

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Refresh(bool includeDetails)
    {
        var summary = downloads.GetQueueSummary();
        SetCounts(summary.TotalCount, summary.ActiveCount, summary.HasPendingOrFailed);
        if (!includeDetails)
        {
            return;
        }

        foreach (var snapshot in downloads.GetDownloads())
        {
            if (_tasksById.TryGetValue(snapshot.Id, out var task))
            {
                task.Update(snapshot);
                continue;
            }

            task = new DownloadTaskViewModel(snapshot);
            _tasksById.Add(task.Id, task);
            Tasks.Add(task);
        }
    }

    private void SetCounts(int total, int active, bool pendingOrFailed)
    {
        if (_totalCount == total && _activeCount == active && HasPendingOrFailedTasks == pendingOrFailed)
        {
            return;
        }

        _totalCount = total;
        _activeCount = active;
        HasPendingOrFailedTasks = pendingOrFailed;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasActiveDownloads)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasTasks)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPendingOrFailedTasks)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }

    internal static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.0} GiB",
            >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.0} MiB",
            >= 1024 => $"{bytes / 1024d:0.0} KiB",
            _ => $"{bytes} B"
        };
    }

    internal static string FormatTransfer(long downloaded, long? total)
    {
        return total is { } size
            ? $"{FormatBytes(downloaded)} / {FormatBytes(size)}"
            : $"已下载 {FormatBytes(downloaded)}";
    }
}

public sealed class DownloadTaskViewModel : INotifyPropertyChanged
{
    private TaskProgress _progress;

    internal DownloadTaskViewModel(DownloadSnapshot snapshot)
    {
        Id = snapshot.Id;
        Name = snapshot.Name;
        _progress = DescribeProgress(snapshot);
        Files = DescribeFiles(snapshot);
    }

    internal Guid Id { get; }
    public string Name { get; }
    public string Status => _progress.Status;
    public string Summary => _progress.Summary;
    public string? Phase => _progress.Phase;
    public bool HasPhase => !string.IsNullOrWhiteSpace(Phase);
    public string? Error => _progress.Error;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string ProgressLabel => _progress.Label;
    public double ProgressValue => _progress.Percent ?? 0;
    public bool HasProgress => _progress.Percent.HasValue;
    public IReadOnlyList<DownloadFileRow> Files { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Update(DownloadSnapshot snapshot)
    {
        var progress = DescribeProgress(snapshot);
        if (_progress != progress)
        {
            _progress = progress;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        var files = DescribeFiles(snapshot);
        if (!Files.SequenceEqual(files))
        {
            Files = files;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Files)));
        }
    }

    private static TaskProgress DescribeProgress(DownloadSnapshot snapshot)
    {
        var status = snapshot.Status switch
        {
            DownloadStatus.Queued => "排队中",
            DownloadStatus.Running => "进行中",
            DownloadStatus.Succeeded => "已完成",
            DownloadStatus.Failed => "失败",
            DownloadStatus.Canceled => "已取消",
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot))
        };
        var processed = snapshot.SucceededFiles + snapshot.FailedFiles;
        var files = snapshot.TotalFiles is { } total ? $"已处理 {processed}/{total} 个文件" : "正在确认文件数量";
        var transfer = DownloadQueueViewModel.FormatTransfer(snapshot.DownloadedBytes, snapshot.TotalBytes);
        var filePercent = snapshot.TotalFiles is > 0
            ? Math.Min(100, processed * 100.0 / snapshot.TotalFiles.Value)
            : (double?)null;
        var percent = snapshot.Status is DownloadStatus.Queued or DownloadStatus.Running
            ? snapshot.Percent ?? filePercent
            : null;
        return new TaskProgress(status, $"{files} · {transfer}",
            snapshot.Percent.HasValue ? "文件下载进度（按大小）" : "文件下载进度（按文件数）",
            percent, snapshot.Phase, snapshot.Error);
    }

    private static IReadOnlyList<DownloadFileRow> DescribeFiles(DownloadSnapshot snapshot)
    {
        return snapshot.Items.Where(item => item.Status is DownloadItemStatus.Downloading
                or DownloadItemStatus.Processing
                or DownloadItemStatus.Paused or DownloadItemStatus.Retrying or DownloadItemStatus.Failed
                or DownloadItemStatus.Canceled)
            .Select(item => new DownloadFileRow(item.Id, item.Name, DescribeFile(item))).ToArray();
    }

    private static string DescribeFile(DownloadItemSnapshot item)
    {
        var state = item.Status switch
        {
            DownloadItemStatus.Processing => "处理中",
            DownloadItemStatus.Paused => "已暂停",
            DownloadItemStatus.Retrying => "等待重试",
            DownloadItemStatus.Failed => "失败",
            DownloadItemStatus.Canceled => "已取消",
            _ => "下载中"
        };
        var transfer = $"{state} · {DownloadQueueViewModel.FormatTransfer(item.DownloadedBytes, item.TotalBytes)}";
        return string.IsNullOrWhiteSpace(item.Error) ? transfer : $"{transfer} · {item.Error}";
    }

    private sealed record TaskProgress(
        string Status,
        string Summary,
        string Label,
        double? Percent,
        string? Phase,
        string? Error);
}

public sealed record DownloadFileRow(Guid Id, string Name, string Summary);
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
    private int _count;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DownloadTaskViewModel> Tasks { get; } = new();
    public bool HasActiveDownloads => _count > 0;
    public string Summary => HasActiveDownloads ? $"当前有 {_count} 项下载任务" : "当前没有下载任务。";

    internal void Refresh(bool includeDetails)
    {
        if (!includeDetails)
        {
            SetCount(downloads.GetActiveDownloadCount());
            return;
        }

        var active = downloads.GetActiveDownloads();
        var activeIds = active.Select(task => task.Id).ToHashSet();
        for (var index = Tasks.Count - 1; index >= 0; index--)
        {
            var task = Tasks[index];
            if (activeIds.Contains(task.Id))
                continue;

            _tasksById.Remove(task.Id);
            Tasks.RemoveAt(index);
        }

        foreach (var snapshot in active)
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

        SetCount(active.Count);
    }

    private void SetCount(int count)
    {
        if (_count == count)
            return;

        _count = count;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasActiveDownloads)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }

    internal static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.0} GiB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.0} MiB",
        >= 1024 => $"{bytes / 1024d:0.0} KiB",
        _ => $"{bytes} B"
    };

    internal static string FormatTransfer(long downloaded, long? total) =>
        total is { } size ? $"{FormatBytes(downloaded)} / {FormatBytes(size)}" : $"已下载 {FormatBytes(downloaded)}";
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

    public event PropertyChangedEventHandler? PropertyChanged;

    internal Guid Id { get; }
    public string Name { get; }
    public string Status => _progress.Status;
    public string Summary => _progress.Summary;
    public string ProgressLabel => _progress.Label;
    public double ProgressValue => _progress.Percent ?? 0;
    public bool HasProgress => _progress.Percent.HasValue;
    public IReadOnlyList<DownloadFileRow> Files { get; private set; }

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
        var status = snapshot.Status == DownloadStatus.Queued ? "排队中" : "进行中";
        var processed = snapshot.SucceededFiles + snapshot.FailedFiles;
        var files = snapshot.TotalFiles is { } total ? $"已处理 {processed}/{total} 个文件" : "正在确认文件数量";
        var transfer = DownloadQueueViewModel.FormatTransfer(snapshot.DownloadedBytes, snapshot.TotalBytes);
        var filePercent = snapshot.TotalFiles is > 0 ? Math.Min(100, processed * 100.0 / snapshot.TotalFiles.Value) : (double?)null;
        return new(status, $"{files} · {transfer}", snapshot.Percent.HasValue ? "下载进度（按大小）" : "下载进度（按文件数）",
            snapshot.Percent ?? filePercent);
    }

    private static IReadOnlyList<DownloadFileRow> DescribeFiles(DownloadSnapshot snapshot) =>
        snapshot.Items.Where(item => item.Status is DownloadItemStatus.Downloading or DownloadItemStatus.Processing
            or DownloadItemStatus.Paused or DownloadItemStatus.Retrying)
            .Select(item => new DownloadFileRow(item.Id, item.Name, DescribeFile(item))).ToArray();

    private static string DescribeFile(DownloadItemSnapshot item)
    {
        var state = item.Status switch
        {
            DownloadItemStatus.Processing => "处理中",
            DownloadItemStatus.Paused => "已暂停",
            DownloadItemStatus.Retrying => "等待重试",
            _ => "下载中"
        };
        return $"{state} · {DownloadQueueViewModel.FormatTransfer(item.DownloadedBytes, item.TotalBytes)}";
    }

    private sealed record TaskProgress(string Status, string Summary, string Label, double? Percent);
}

public sealed record DownloadFileRow(Guid Id, string Name, string Summary);

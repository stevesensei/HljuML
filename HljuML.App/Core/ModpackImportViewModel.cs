using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HljuML.Core.GamePlay;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Shared.Model;
using Microsoft.Extensions.Logging;

namespace HljuML.App.Core;

/// <summary>拖入与手动导入共用的确认流程；解析期间持有快照，确认后才创建下载任务。</summary>
public sealed class ModpackImportViewModel(ModpackImportService imports, JavaRuntimeService runtimes,
    ILogger<ModpackImportViewModel> logger, CancellationToken stopping) : INotifyPropertyChanged, IDisposable
{
    private ModpackImportSession? _session;
    private CancellationTokenSource? _cancellation;
    private TaskCompletionSource? _closed;
    private Task? _loadTask;
    private string _file = "";
    private string _instanceName = "";
    private bool _isOpen;
    private bool _downloadJava;
    private bool _javaResolved;
    private bool _disposed;
    private int? _requiredJava;

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            if (_isOpen == value) return;
            _isOpen = value;
            if (!value) { _cancellation?.Cancel(); _closed?.TrySetResult(); }
            NotifyAll();
        }
    }
    public string InstanceName
    {
        get => _instanceName;
        set { _instanceName = value; NameError = null; NotifyAll(); }
    }
    public bool DownloadJava
    {
        get => _downloadJava;
        set { _downloadJava = value; NotifyAll(); }
    }
    public bool IsLoading { get; private set; }
    public bool IsSubmitting { get; private set; }
    public bool CanDismiss => !IsSubmitting;
    public bool CanConfirm => _session is not null && _javaResolved && !IsLoading && !IsSubmitting;
    public bool CanRetry => Error is not null && !IsLoading && !IsSubmitting;
    public bool HasPack => _session is not null;
    public bool HasJavaRequirement => _requiredJava is > 0;
    public bool HasError => Error is not null;
    public string? Error { get; private set; }
    public string? NameError { get; private set; }
    public string PackDescription => _session is { } session
        ? $"{session.Info.Name} · {session.Info.PackVersion}\nMinecraft {session.Info.MinecraftVersion} · {session.Info.Loader} {session.Info.LoaderVersion}"
        : Path.GetFileName(_file);
    public string JavaStatus { get; private set; } = "正在读取 Java 要求…";
    public string DownloadJavaLabel => $"下载并为此实例使用 Java {_requiredJava}";
    public string Destination => LauncherPaths.DefaultInstanceDirectory;

    /// <summary>顺序处理一份文件，直至用户取消或成功提交；取消解析不会取消已经入队的安装。</summary>
    public async Task OpenAsync(string file)
    {
        if (_disposed || _closed is not null) return;
        _file = file;
        _instanceName = "";
        _requiredJava = null;
        _javaResolved = false;
        _downloadJava = false;
        JavaStatus = "正在读取 Java 要求…";
        Error = NameError = null;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        var closed = _closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IsOpen = true;
        try
        {
            await LoadAsync();
            await closed.Task.WaitAsync(stopping);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { IsOpen = false; }
        finally
        {
            // 取消重试时也等待解析真正结束，再释放快照和令牌，避免与下一份包交叉写入状态。
            _cancellation.Cancel();
            if (_loadTask is { } loading) await loading;
            _loadTask = null;
            _session?.Dispose();
            _session = null;
            _cancellation.Dispose();
            _cancellation = null;
            _closed = null;
            NotifyAll();
        }
    }

    /// <summary>先解析快照，再解析 Minecraft 版本文件；重试保留已编辑的实例名称。</summary>
    public Task LoadAsync()
    {
        if (!IsOpen || IsLoading || IsSubmitting || _cancellation is null) return Task.CompletedTask;
        return _loadTask = LoadCoreAsync();
    }

    private async Task LoadCoreAsync()
    {
        if (!IsOpen || IsLoading || IsSubmitting || _cancellation is null) return;
        var token = _cancellation.Token;
        IsLoading = true;
        Error = null;
        NotifyAll();
        try
        {
            if (_session is null)
            {
                var result = await imports.PrepareAsync(_file, token);
                _session = result.Data;
                token.ThrowIfCancellationRequested();
                if (_session is null) { Error = result.Message; return; }
                _instanceName = _session.SuggestedName;
                NotifyAll();
            }
            _requiredJava = await runtimes.GetRequiredMajorAsync(_session.Info.MinecraftVersion, token);
            if (_requiredJava is { } major)
            {
                var installed = await runtimes.FindExactAsync(major, token);
                _downloadJava = installed is null;
                JavaStatus = installed is null
                    ? $"版本文件要求 Java {major}，本机尚未找到对应版本。取消勾选后可稍后手动配置。"
                    : $"版本文件要求 Java {major}，将使用已安装的 {installed.Version}。\n{installed.Path}";
            }
            else JavaStatus = "版本文件未声明 Java 要求，请在导入后自行配置适合该整合包的 Java 环境。";
            _javaResolved = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not prepare modpack import {File}", _file);
            Error = $"无法解析导入信息：{error.Message} 请重试。";
        }
        finally { IsLoading = false; NotifyAll(); }
    }

    /// <summary>提交时沿用应用生命周期令牌；窗口随后关闭不会取消安装队列中的任务。</summary>
    public async Task ConfirmAsync()
    {
        if (!CanConfirm || _session is null) return;
        if (string.IsNullOrWhiteSpace(InstanceName) || InstanceName.Trim().Length > 128
            || System.Linq.Enumerable.Any(InstanceName, char.IsControl))
        {
            NameError = "请输入有效的实例名称（最多 128 个字符）。";
            NotifyAll();
            return;
        }
        IsSubmitting = true;
        Error = NameError = null;
        NotifyAll();
        try
        {
            var result = await imports.EnqueueAsync(_session, InstanceName.Trim(), _requiredJava,
                DownloadJava, cancellationToken: stopping);
            if (result.Status == ServiceStatus.Success) IsOpen = false;
            else NameError = result.Message;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { IsOpen = false; }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not confirm modpack import {File}", _file);
            Error = $"无法提交导入：{error.Message}";
        }
        finally { IsSubmitting = false; NotifyAll(); }
    }

    /// <summary>统一通知模态框各字段；所有调用由 UI 线程发起并在其上下文恢复。</summary>
    private void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void Dispose()
    {
        _disposed = true;
        IsOpen = false;
        _session?.Dispose();
    }
}

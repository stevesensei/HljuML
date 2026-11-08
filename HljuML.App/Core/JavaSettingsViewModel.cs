using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using HljuML.Core.GamePlay;
using TKCode.Arc;

namespace HljuML.App.Core;

public sealed class JavaSettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly JavaEnvService _java;
    private readonly CancellationToken _stopping;
    private string _selectedPath = "";
    private bool _refreshing;
    private bool _disposed;
    private string? _error;
    private bool _isChoosingJava;

    public static SelectOption BrowseJavaOption { get; } = new(new object(), "手动选择 Java…");

    public JavaSettingsViewModel(JavaEnvService java, CancellationToken stopping)
    {
        _java = java;
        _stopping = stopping;
        java.Changed += OnJavaChanged;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<SelectOption> JavaOptions { get; private set; } = [];
    public bool IsScanning { get; private set; }
    public bool CanScan => !IsScanning && !_isChoosingJava;
    public string ScanStatus { get; private set; } = "";
    public string? ErrorMessage => _error;
    public bool HasError => _error is not null;
    public string SelectedPathDescription => _selectedPath.Length == 0
        ? "启动游戏时按游戏版本自动匹配 Java。"
        : _selectedPath;

    public SelectOption? SelectedJavaOption
    {
        get => JavaOptions.FirstOrDefault(option => Equals(option.Value, _selectedPath));
        set
        {
            if (value?.Value is not string path || _refreshing || path == _selectedPath)
                return;

            try
            {
                _java.SetDefaultJavaPath(path);
                _selectedPath = path;
                _error = null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _error = error is ArgumentException ? error.Message : "无法保存 Java 设置，请检查启动器目录权限后重试。";
            }
            Notify(nameof(SelectedJavaOption));
            Notify(nameof(SelectedPathDescription));
            Notify(nameof(ErrorMessage));
            Notify(nameof(HasError));
        }
    }

    public async Task ScanAsync()
    {
        try
        {
            await _java.ScanAsync(_stopping);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }

    public async Task ChooseJavaAsync(Func<Task<string?>> chooseFile)
    {
        if (!CanScan) return;
        _isChoosingJava = true;
        _error = null;
        Notify(nameof(CanScan));
        Notify(nameof(ErrorMessage));
        Notify(nameof(HasError));
        try
        {
            await Task.Yield();
            var path = await chooseFile();
            if (path is not null && !_disposed)
            {
                await _java.AddManualJavaAsync(path, _stopping);
                Refresh();
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException)
        {
            _error = error is ArgumentException or NotSupportedException ? error.Message
                : "无法读取或保存 Java 环境，请检查文件及目录权限后重试。";
        }
        finally
        {
            _isChoosingJava = false;
            if (!_disposed)
            {
                Notify(nameof(CanScan));
                Notify(nameof(SelectedJavaOption));
                Notify(nameof(SelectedPathDescription));
                Notify(nameof(ErrorMessage));
                Notify(nameof(HasError));
            }
        }
    }

    private void OnJavaChanged() => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
            Refresh();
    });

    private void Refresh()
    {
        var environments = _java.Environments;
        var selected = _java.DefaultJavaPath;
        var options = new List<SelectOption> { new("", "自动匹配游戏所需的 Java") };
        options.AddRange(environments.Select(java =>
            new SelectOption(java.Path, $"{java.Type} {java.Version} · {java.Arch} · {java.Path}")));
        if (selected.Length > 0 && !environments.Any(java => java.Path == selected))
            options.Add(new SelectOption(selected, $"已保存的 Java（未扫描到） · {selected}"));
        options.Add(BrowseJavaOption);

        _refreshing = true;
        try
        {
            var optionsChanged = !JavaOptions.Select(option => (option.Value, option.Label))
                .SequenceEqual(options.Select(option => (option.Value, option.Label)));
            if (optionsChanged)
                JavaOptions = options;
            _selectedPath = selected;
            IsScanning = _java.IsScanning;
            ScanStatus = IsScanning ? "正在扫描已安装的 Java…"
                : environments.Count > 0 ? $"已找到 {environments.Count} 个 Java 环境。每次打开启动器时自动扫描。"
                : "尚未找到 Java。可以扫描，或在下拉框中手动选择 Java 文件。";
            _error = _java.ScanError;
            if (optionsChanged)
                Notify(nameof(JavaOptions));
            foreach (var property in new[] { nameof(SelectedJavaOption),
                nameof(SelectedPathDescription), nameof(IsScanning), nameof(CanScan), nameof(ScanStatus), nameof(ErrorMessage),
                nameof(HasError) })
                Notify(property);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _disposed = true;
        _java.Changed -= OnJavaChanged;
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ColorMC.Core.Objs;
using HljuML.Core.Infrastructure;
using HljuML.Core.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using TKCode.Arc;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace HljuML.App.Core;

public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly LauncherSettingsService _settings;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _disposed;
    private LauncherBackground? _backgroundMode;
    private int _imageRequest;
    private string _section = "Game";
    private string _memory, _threads, _limit, _jvm, _mua;
    private readonly Dictionary<string, string?> _errors = new();

    public SettingsViewModel(LauncherSettingsService settings, JavaSettingsViewModel java,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _logger = logger;
        Java = java;
        var value = settings.Value;
        _memory = value.MaximumMemoryGb.ToString(CultureInfo.CurrentCulture);
        _threads = value.DownloadThreads.ToString(CultureInfo.CurrentCulture);
        _limit = value.DownloadLimitMb.ToString(CultureInfo.CurrentCulture);
        _jvm = value.JvmArguments;
        _mua = value.MuaOrganizationId;
        ErrorMessage = settings.LoadError;
        settings.Changed += OnSettingsChanged;
        UpdateBackground();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public JavaSettingsViewModel Java { get; }
    public string HljuMLVersion => LauncherInfo.Version;
    public string ColorMCVersion => LauncherInfo.CoreFrameworkVersion;
    public string ImagesDirectory => LauncherSettingsService.ImagesDirectory;
    public Bitmap? BackgroundImage { get; private set; }
    public string BackgroundStatus { get; private set; } = "";
    public string? ErrorMessage { get; private set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsGame => _section == "Game";
    public bool IsLauncher => _section == "Launcher";
    public bool IsAbout => _section == "About";
    public void SelectSection(string section)
    {
        _section = section;
        Notify(nameof(IsGame)); Notify(nameof(IsLauncher)); Notify(nameof(IsAbout));
    }

    public IReadOnlyList<SelectOption> StartOptions { get; } =
    [new(LauncherStartBehavior.KeepOpen, "保持不变"), new(LauncherStartBehavior.Minimize, "最小化启动器"), new(LauncherStartBehavior.Close, "自动关闭启动器")];
    public IReadOnlyList<SelectOption> WindowOptions { get; } =
    [new(GameWindowMode.Default, "默认"), new(GameWindowMode.Fullscreen, "全屏"),
        new(GameWindowMode.Maximized, OperatingSystem.IsWindows() ? "最大化" : "最大化（当前平台不可用）") { IsDisabled = !OperatingSystem.IsWindows() }];
    public IReadOnlyList<SelectOption> SourceOptions { get; } =
    [new(SourceLocal.Offical, "官方"), new(SourceLocal.BMCLAPI, "BMCL API 镜像源")];
    public IReadOnlyList<SelectOption> CommunityOptions { get; } =
    [new(CommunityDownloadSource.Official, "官方"), new(CommunityDownloadSource.Mcim, "MCIM 镜像源（占位）") { IsDisabled = true }];
    public IReadOnlyList<SelectOption> BackgroundOptions { get; } =
    [new(LauncherBackground.Default, "默认"), new(LauncherBackground.RandomImage, "随机图片")];
    public IReadOnlyList<SelectOption> ColorOptions { get; } =
    [new("sky", "天蓝"), new("leaf", "绿叶（占位）") { IsDisabled = true }];
    public SelectOption SelectedColor => ColorOptions[0];
    public IReadOnlyList<SelectOption> LogLevelOptions { get; } = Enum.GetValues<LogLevel>()
        .Where(level => level != LogLevel.None).Select(level => new SelectOption(level, level.ToString())).ToArray();
    private static SelectOption? Find(IEnumerable<SelectOption> options, object value) => options.FirstOrDefault(option => Equals(option.Value, value));
    public SelectOption? SelectedStart { get => Find(StartOptions, _settings.Value.AfterGameStart); set => Choice(value, (s, v) => s with { AfterGameStart = (LauncherStartBehavior)v }); }
    public SelectOption? SelectedWindow { get => Find(WindowOptions, _settings.Value.GameWindow); set => Choice(value, (s, v) => s with { GameWindow = (GameWindowMode)v }); }
    public SelectOption? SelectedSource { get => Find(SourceOptions, _settings.Value.MinecraftSource); set => Choice(value, (s, v) => s with { MinecraftSource = (SourceLocal)v }); }
    public SelectOption? SelectedCommunity { get => Find(CommunityOptions, _settings.Value.CommunitySource); set => Choice(value, (s, v) => s with { CommunitySource = (CommunityDownloadSource)v }); }
    public SelectOption? SelectedBackground { get => Find(BackgroundOptions, _settings.Value.Background); set => Choice(value, (s, v) => s with { Background = (LauncherBackground)v }); }
    public SelectOption? SelectedLogLevel { get => Find(LogLevelOptions, _settings.Value.MinimumLogLevel); set => Choice(value, (s, v) => s with { MinimumLogLevel = (LogLevel)v }); }
    public bool DebugMode { get => _settings.Value.DebugMode; set => Save(s => s with { DebugMode = value }); }
    private void Choice(SelectOption? option, Func<LauncherSettings, object, LauncherSettings> change)
    {
        if (option is not null && !option.IsDisabled) Save(s => change(s, option.Value!));
    }

    public string Memory { get => _memory; set { _memory = value; Notify(nameof(Memory)); } }
    public string Threads { get => _threads; set { _threads = value; Notify(nameof(Threads)); } }
    public string Limit { get => _limit; set { _limit = value; Notify(nameof(Limit)); } }
    public string Jvm { get => _jvm; set { _jvm = value; Notify(nameof(Jvm)); } }
    public string Mua { get => _mua; set { _mua = value; Notify(nameof(Mua)); } }
    public string? MemoryError => _errors.GetValueOrDefault("Memory");
    public string? ThreadsError => _errors.GetValueOrDefault("Threads");
    public string? LimitError => _errors.GetValueOrDefault("Limit");
    public string? JvmError => _errors.GetValueOrDefault("Jvm");
    public string? MuaError => _errors.GetValueOrDefault("Mua");

    /// <summary>文本完成编辑后提交，避免输入小数或多行参数时反复写入半成品。</summary>
    public void Commit(string field)
    {
        Save(s => field switch
        {
            "Memory" => s with { MaximumMemoryGb = ParseDecimal(Memory, "最大内存请输入数字，单位 G。") },
            "Threads" => s with { DownloadThreads = int.TryParse(Threads, out var count) ? count : throw new ArgumentException("下载线程数请输入 1–128 的整数。") },
            "Limit" => s with { DownloadLimitMb = ParseDecimal(Limit, "限速请输入数字，单位 M/s，0 表示不限速。") },
            "Jvm" => s with { JvmArguments = Jvm.Replace("\r\n", "\n").Trim() },
            "Mua" => s with { MuaOrganizationId = Mua.Trim() },
            _ => s
        }, field);
    }
    private static decimal ParseDecimal(string text, string error) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var value)
            ? value : throw new ArgumentException(error);
    private void Save(Func<LauncherSettings, LauncherSettings> change, string? field = null)
    {
        try
        {
            _settings.Update(change);
            ErrorMessage = null;
            if (field is not null) _errors[field] = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ErrorMessage = error is ArgumentException ? error.Message : "无法保存设置，请检查启动器目录权限后重试。";
            if (field is not null) _errors[field] = ErrorMessage;
            _logger.LogWarning(error, "保存启动器设置失败");
        }
        if (field is not null) Notify(field + "Error");
        foreach (var name in new[] { nameof(ErrorMessage), nameof(HasError), nameof(DebugMode), nameof(SelectedStart),
            nameof(SelectedWindow), nameof(SelectedSource), nameof(SelectedCommunity), nameof(SelectedBackground), nameof(SelectedLogLevel) }) Notify(name);
    }

    private void OnSettingsChanged() => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed) { UpdateBackground(); Notify(nameof(DebugMode)); }
    });
    private async void UpdateBackground()
    {
        var mode = _settings.Value.Background;
        if (_backgroundMode == mode) return;
        _backgroundMode = mode;
        var request = ++_imageRequest;
        Bitmap? image = null;
        if (mode == LauncherBackground.RandomImage)
        {
            image = await Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(ImagesDirectory);
                    foreach (var path in Directory.EnumerateFiles(ImagesDirectory).OrderBy(_ => Random.Shared.Next()))
                    {
                        if (!new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
                        try
                        {
                            if (new FileInfo(path).Length > 32 * 1024 * 1024) continue;
                            using var stream = File.OpenRead(path);
                            return Bitmap.DecodeToWidth(stream, 2560);
                        }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
                            or NotSupportedException or InvalidOperationException)
                        { _logger.LogWarning(error, "无法读取背景图片 {Path}", path); }
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { _logger.LogWarning(error, "无法读取自定义背景目录"); }
                return null;
            });
        }
        if (_disposed || request != _imageRequest) { image?.Dispose(); return; }
        var previous = BackgroundImage;
        BackgroundImage = image;
        BackgroundStatus = mode == LauncherBackground.Default ? "使用默认天蓝背景。"
            : image is null ? "目录中没有可读取的图片，已使用默认背景。支持 PNG、JPEG、WebP、BMP（单张不超过 32 MB）。"
            : "已随机选择一张图片；下次启动或重新切换时更换。";
        Notify(nameof(BackgroundImage)); Notify(nameof(BackgroundStatus));
        if (previous is not null) Dispatcher.UIThread.Post(previous.Dispose, DispatcherPriority.Background);
    }
    private void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    public void Dispose()
    {
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        BackgroundImage?.Dispose();
    }
}

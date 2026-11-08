using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ColorMC.Core.Config;
using ColorMC.Core.Net;
using ColorMC.Core.Objs;
using HljuML.Core.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace HljuML.Core.Infrastructure.Settings;

public enum LauncherStartBehavior { KeepOpen, Minimize, Close }
public enum GameWindowMode { Default, Fullscreen, Maximized }
public enum LauncherBackground { Default, RandomImage }
public enum CommunityDownloadSource { Official, Mcim }

public sealed record LauncherSettings
{
    public LauncherStartBehavior AfterGameStart { get; init; }
    public GameWindowMode GameWindow { get; init; }
    public decimal MaximumMemoryGb { get; init; } = 4;
    public string JvmArguments { get; init; } = "";
    public SourceLocal MinecraftSource { get; init; } = SourceLocal.BMCLAPI;
    public CommunityDownloadSource CommunitySource { get; init; }
    public int DownloadThreads { get; init; } = 64;
    public decimal DownloadLimitMb { get; init; }
    public string MuaOrganizationId { get; init; } = "";
    public bool DebugMode { get; init; }
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;
    public LauncherBackground Background { get; init; }
}

/// <summary>启动器设置独立保存；不可变快照允许下载线程安全读取最新配置。</summary>
public sealed class LauncherSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };
    private readonly object _sync = new();
    private LauncherSettings _value = new();
    private bool _frameworkReady;
    public LauncherSettings Value => Volatile.Read(ref _value);
    public string? LoadError { get; private set; }
    public event Action? Changed;
    public static string ImagesDirectory => Path.Combine(LauncherPaths.ColorMcDirectory, "custom", "images");
    private static string SettingsPath => Path.Combine(LauncherPaths.ColorMcDirectory, "launcher-settings.json");

    public LauncherSettingsService()
    {
        try
        {
            Directory.CreateDirectory(ImagesDirectory);
            if (File.Exists(SettingsPath))
            {
                var value = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath), JsonOptions)
                    ?? throw new InvalidDataException("设置文件内容为空。");
                Validate(value);
                _value = value;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            LoadError = $"无法读取设置，已使用默认值。请检查 {SettingsPath}。{error.Message}";
        }
    }

    public void Update(Func<LauncherSettings, LauncherSettings> change)
    {
        lock (_sync)
        {
            var next = change(_value);
            Validate(next);
            if (next == _value) return;
            Directory.CreateDirectory(LauncherPaths.ColorMcDirectory);
            var temporary = SettingsPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(next, JsonOptions));
                File.Move(temporary, SettingsPath, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Volatile.Write(ref _value, next);
            LoadError = null;
            if (_frameworkReady) ApplyFrameworkDefaults();
        }
        Changed?.Invoke();
    }

    /// <summary>只写 ColorMC 全局默认值；核心在生成参数时仍优先读取实例配置。</summary>
    public void AttachFramework()
    {
        lock (_sync)
        {
            _frameworkReady = true;
            ApplyFrameworkDefaults();
        }
    }

    private void ApplyFrameworkDefaults()
    {
        var value = Value;
        var config = ConfigLoad.Config;
        config.Http.Source = value.MinecraftSource;
        config.Http.DownloadThread = value.DownloadThreads;
        CoreHttpClient.Source = value.MinecraftSource;
        config.DefaultJvmArg.MaxMemory = (uint)(value.MaximumMemoryGb * 1024);
        // 保证默认最小内存不超过用户指定的最大内存。
        config.DefaultJvmArg.MinMemory = Math.Min(512u, config.DefaultJvmArg.MaxMemory.Value);
        config.DefaultJvmArg.JvmArgs = value.JvmArguments;
        config.Window.FullScreen = value.GameWindow == GameWindowMode.Fullscreen;
    }

    public static void Validate(LauncherSettings value)
    {
        if (value.MaximumMemoryGb is < 0.25m or > 1024m)
            throw new ArgumentException("最大内存请输入 0.25–1024 G。实例自己的内存设置优先。");
        if (value.DownloadThreads is < 1 or > 128)
            throw new ArgumentException("下载线程数请输入 1–128 的整数。");
        if (value.DownloadLimitMb is < 0 or > 100000)
            throw new ArgumentException("下载限速请输入 0–100000 M/s，0 表示不限速。");
        if (value.JvmArguments is null || value.JvmArguments.Length > 32768)
            throw new ArgumentException("JVM 参数不能超过 32768 个字符。");
        if (Regex.IsMatch(value.JvmArguments, @"(^|\s)-Xm[sx]", RegexOptions.IgnoreCase))
            throw new ArgumentException("请通过内存分配设置最大内存，不要在 JVM 参数中填写 -Xmx 或 -Xms。");
        if (value.MuaOrganizationId is null || value.MuaOrganizationId.Length > 256)
            throw new ArgumentException("MUA 组织 ID 不能超过 256 个字符。");
        if (!Enum.IsDefined(value.AfterGameStart) || !Enum.IsDefined(value.GameWindow)
            || !Enum.IsDefined(value.Background) || !Enum.IsDefined(value.CommunitySource)
            || value.MinecraftSource is not (SourceLocal.Offical or SourceLocal.BMCLAPI)
            || value.MinimumLogLevel is < LogLevel.Trace or > LogLevel.Critical)
            throw new ArgumentException("设置包含不支持的选项，请重新选择。");
    }
}

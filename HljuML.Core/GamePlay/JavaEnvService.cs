using System.Text.Json;
using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Objs;
using ColorMC.Core.Utils;
using HljuML.Core.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.GamePlay;

public sealed class JavaEnvService : BackgroundService
{
    private readonly ILogger<JavaEnvService> _logger;
    private readonly object _sync = new();
    private Task<IReadOnlyList<JavaInfoObj>>? _scanTask;
    private IReadOnlyList<JavaInfoObj> _environments = [];
    private string _defaultJavaPath = "";
    private bool _isScanning;
    private string? _scanError;

    public JavaEnvService(ILogger<JavaEnvService> logger)
    {
        _logger = logger;
        try
        {
            if (File.Exists(LauncherPaths.JavaSettingsPath))
                _defaultJavaPath = JsonSerializer.Deserialize<JavaPreferences>(
                    File.ReadAllText(LauncherPaths.JavaSettingsPath))?.DefaultJavaPath ?? "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(error, "Could not read the default Java setting");
            _scanError = "无法读取 Java 设置，请重新选择默认环境。";
        }
    }

    public event Action? Changed;

    public IReadOnlyList<JavaInfoObj> Environments
    {
        get { lock (_sync) return _environments; }
    }

    public string DefaultJavaPath
    {
        get { lock (_sync) return _defaultJavaPath; }
    }

    public bool IsScanning
    {
        get { lock (_sync) return _isScanning; }
    }

    public string? ScanError
    {
        get { lock (_sync) return _scanError; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ScanAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public Task<IReadOnlyList<JavaInfoObj>> ScanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<IReadOnlyList<JavaInfoObj>> scan;
        lock (_sync)
        {
            if (!_isScanning)
            {
                _isScanning = true;
                _scanTask = Task.Run(ScanCoreAsync);
            }
            scan = _scanTask!;
        }
        Changed?.Invoke();
        return scan.WaitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<JavaInfoObj>> ScanCoreAsync()
    {
        try
        {
            var found = await JavaHelper.FindJava() ?? [];
            var paths = new List<string> { DefaultJavaPath };
            if (Environment.GetEnvironmentVariable("JAVA_HOME") is { Length: > 0 } javaHome)
                paths.Add(Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "javaw.exe" : "java"));
            if (Directory.Exists(JvmPath.JavaDir))
                found.AddRange(await Task.Run(() => JavaHelper.FindJava(JvmPath.JavaDir) ?? []));

            foreach (var path in paths.Where(File.Exists))
            {
                if (await Task.Run(() => JavaHelper.GetJavaInfo(path)) is { } java)
                    found.Add(java);
            }

            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var environments = found.Where(java => File.Exists(java.Path))
                .DistinctBy(java => Path.GetFullPath(java.Path), comparer)
                .OrderByDescending(java => java.MajorVersion)
                .ThenBy(java => java.Version).ThenBy(java => java.Path, comparer).ToArray();
            lock (_sync)
            {
                _environments = environments;
                _scanError = null;
            }
            return environments;
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Could not scan Java environments");
            lock (_sync) _scanError = "扫描 Java 失败，请检查安装目录权限后重试。";
            return Environments;
        }
        finally
        {
            lock (_sync) _isScanning = false;
            Changed?.Invoke();
        }
    }

    public void SetDefaultJavaPath(string path)
    {
        lock (_sync)
        {
            if (path.Length > 0 && (!Path.IsPathFullyQualified(path) || !File.Exists(path)
                || !_environments.Any(java => string.Equals(java.Path, path, OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
                throw new ArgumentException("请选择扫描到的可用 Java 环境。", nameof(path));

            Directory.CreateDirectory(LauncherPaths.ColorMcDirectory);
            var temporary = Path.Combine(LauncherPaths.ColorMcDirectory, $".java-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(new JavaPreferences(path)));
                File.Move(temporary, LauncherPaths.JavaSettingsPath, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
            _defaultJavaPath = path;
        }
        Changed?.Invoke();
    }

    public async Task<JavaInfoObj?> FindForMinecraftVersionAsync(string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftVersion);
        if (minecraftVersion.IndexOfAny(['/', '\\']) >= 0 || minecraftVersion.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || minecraftVersion is "." or "..")
        {
            throw new ArgumentException("Invalid Minecraft version.", nameof(minecraftVersion));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var version = VersionPath.GetVersion(minecraftVersion)
            ?? await CheckHelpers.CheckGameArgFileAsync(minecraftVersion);
        cancellationToken.ThrowIfCancellationRequested();
        return version?.JavaVersion?.MajorVersion is > 0 and var majorVersion
            ? await FindJavaAsync(majorVersion, cancellationToken)
            : null;
    }

    public async Task<JavaInfoObj?> FindJavaAsync(int majorVersion, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(majorVersion);
        cancellationToken.ThrowIfCancellationRequested();

        var registered = FindRegisteredJava(majorVersion);
        if (registered?.MajorVersion == majorVersion)
            return registered;

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        JavaInfoObj? fromEnvironment = null;
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var executable = Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
            fromEnvironment = await Task.Run(() => JavaHelper.GetJavaInfo(executable), cancellationToken);
            if (fromEnvironment?.MajorVersion == majorVersion)
                return fromEnvironment;
        }

        IReadOnlyList<JavaInfoObj> discovered;
        lock (_sync) discovered = _environments;
        if (discovered.Count == 0 || IsScanning)
            discovered = await ScanAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = discovered.Concat(new[] { registered, fromEnvironment }).OfType<JavaInfoObj>();
        return MatchInstalledJava(candidates, majorVersion);
    }

    private static JavaInfoObj? FindRegisteredJava(int majorVersion)
    {
        try
        {
            if (JvmPath.FindJava(majorVersion) is { } java && java.GetPath() is { } path && File.Exists(path))
                return java with { Path = path };

            return MatchInstalledJava(JvmPath.Jvms.Values.Select(item => item with { Path = item.GetPath() }), majorVersion);
        }
        catch (InvalidOperationException)
        {
            // ColorMC can populate its registry in the background. Fall back to its scanner during that scan.
        }

        return null;
    }

    private static JavaInfoObj? MatchInstalledJava(IEnumerable<JavaInfoObj> candidates, int majorVersion) => candidates
        .Where(item => File.Exists(item.Path) && (item.MajorVersion == majorVersion
            || majorVersion > 8 && item.MajorVersion > majorVersion))
        .OrderByDescending(item => item.MajorVersion == majorVersion)
        .ThenByDescending(item => item.Arch == SystemInfo.SystemArch)
        .ThenByDescending(item => item.MajorVersion)
        .FirstOrDefault();

    private sealed record JavaPreferences(string DefaultJavaPath);
}

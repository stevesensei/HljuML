using System.Text.Json;
using ColorMC.Core.Helpers;
using ColorMC.Core.Objs;
using HljuML.Core.Infrastructure.Storage;

namespace HljuML.Core.GamePlay;

public sealed partial class JavaEnvService
{
    public async Task AddManualJavaAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = Path.GetFileName(path);
        var validName = OperatingSystem.IsWindows()
            ? name.Equals("javaw.exe", StringComparison.OrdinalIgnoreCase) || name.Equals("java.exe", StringComparison.OrdinalIgnoreCase)
            : name == "java";
        if (!Path.IsPathFullyQualified(path) || !validName || !File.Exists(path))
            throw new ArgumentException("请选择存在的 javaw.exe 或 java.exe 文件。");

        path = Path.GetFullPath(path);
        var java = await Task.Run(() => JavaHelper.GetJavaInfo(path), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (java is null || java.MajorVersion <= 0)
            throw new ArgumentException("无法读取该文件的 Java 版本，请选择有效的 Java 可执行文件。");

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        lock (_sync)
        {
            var manualPaths = _manualJavaPaths.Append(path).Distinct(comparer).ToArray();
            SavePreferences(path, manualPaths);
            _manualJavaPaths = manualPaths;
            _environments = new[] { java with { Path = path } }.Concat(_environments)
                .DistinctBy(item => Path.GetFullPath(item.Path), comparer)
                .OrderByDescending(item => item.MajorVersion).ThenBy(item => item.Version)
                .ThenBy(item => item.Path, comparer).ToArray();
            _defaultJavaPath = path;
            _scanError = null;
        }
        Changed?.Invoke();
    }

    private static void SavePreferences(string defaultPath, IReadOnlyList<string> manualPaths)
    {
        Directory.CreateDirectory(LauncherPaths.ColorMcDirectory);
        var temporary = Path.Combine(LauncherPaths.ColorMcDirectory, $".java-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new JavaPreferences(defaultPath, manualPaths)));
            File.Move(temporary, LauncherPaths.JavaSettingsPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}

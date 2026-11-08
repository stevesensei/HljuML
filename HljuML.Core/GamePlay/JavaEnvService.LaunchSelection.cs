using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Objs;
using ColorMC.Core.Utils;

namespace HljuML.Core.GamePlay;

public sealed record JavaLaunchSelection(JavaInfoObj Java, int RecommendedMajor)
{
    public bool NeedsWarning => Java.MajorVersion != RecommendedMajor;
}

public sealed partial class JavaEnvService
{
    /// <summary>优先读取已安装的版本元数据；自定义实例取各组件最低要求的最大值。</summary>
    public static async Task<int> GetRequiredLaunchMajorAsync(GameSettingObj game, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (game.CustomLoader?.CustomJson == true && game.CustomJson is { Count: > 0 })
        {
            var requirements = game.CustomJson.Select(item => item.JavaVersion?.MajorVersion
                ?? item.CompatibleJavaMajors?.Where(major => major > 0).DefaultIfEmpty(0).Min() ?? 0)
                .Where(major => major > 0).ToArray();
            if (requirements.Length > 0) return requirements.Max();
        }

        var id = game.Version;
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(['/', '\\']) >= 0
            || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || id is "." or "..")
            throw new InvalidDataException("实例的 Minecraft 版本标识无效，请检查实例配置。");
        var metadata = VersionPath.GetVersion(id)
            ?? await CheckHelpers.CheckGameArgFileAsync(id).WaitAsync(token);
        token.ThrowIfCancellationRequested();
        return metadata?.JavaVersion?.MajorVersion is > 0 and var majorVersion ? majorVersion
            : throw new InvalidDataException("无法读取实例要求的 Java 版本，请检查 Minecraft 版本文件后重试。");
    }

    /// <summary>选择不低于推荐版本且主版本差值最小的 Java，仅覆盖本次启动路径。</summary>
    public async Task<JavaLaunchSelection?> SelectForLaunchAsync(GameSettingObj game, int minimum,
        CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum);
        token.ThrowIfCancellationRequested();
        if (IsScanning || Environments.Count == 0) await ScanAsync(token);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var preferred = new List<string>();
        if (!string.IsNullOrWhiteSpace(game.JvmLocal)) preferred.Add(game.JvmLocal);
        if (!string.IsNullOrWhiteSpace(game.JvmName) && JvmPath.GetInfo(game.JvmName) is { } named)
            preferred.Add(named.GetPath());
        if (DefaultJavaPath is { Length: > 0 } defaultPath) preferred.Add(defaultPath);
        var paths = preferred.Where(Path.IsPathFullyQualified).Select(JavaExecutablePreference.PreferWindowed)
            .Distinct(comparer).ToArray();
        var candidates = Environments.Where(item => Path.IsPathFullyQualified(item.Path))
            .Select(item => item with { Path = JavaExecutablePreference.PreferWindowed(item.Path) })
            .Where(item => File.Exists(item.Path)).DistinctBy(item => item.Path, comparer)
            .ToDictionary(item => item.Path, comparer);
        var verified = new HashSet<string>(comparer);

        // 外部实例可能指定扫描目录之外的 Java；验证后也加入可见列表，避免忽略可用环境。
        foreach (var path in paths)
        {
            var java = File.Exists(path) ? await Task.Run(() => JavaHelper.GetJavaInfo(path), token) : null;
            if (java is not null)
            {
                candidates[path] = java with { Path = path };
                verified.Add(path);
            }
            else candidates.Remove(path);
            UpdateLaunchEnvironment(path, java, comparer);
        }

        while (SelectClosestForLaunch(candidates.Values, minimum, paths) is { } candidate)
        {
            token.ThrowIfCancellationRequested();
            if (verified.Contains(candidate.Path)) return new(candidate, minimum);
            var actual = await Task.Run(() => JavaHelper.GetJavaInfo(candidate.Path), token);
            UpdateLaunchEnvironment(candidate.Path, actual, comparer);
            if (actual is null) candidates.Remove(candidate.Path);
            else
            {
                candidates[candidate.Path] = actual with { Path = candidate.Path };
                verified.Add(candidate.Path);
            }
            // 可执行文件可能已被替换；更新实际版本后重新排序，不能使用过期扫描结果。
        }
        return null;
    }

    internal static JavaInfoObj? SelectClosestForLaunch(IEnumerable<JavaInfoObj> candidates, int minimum,
        IReadOnlyList<string> preferredPaths)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        int Preference(JavaInfoObj java)
        {
            for (var index = 0; index < preferredPaths.Count; index++)
                if (comparer.Equals(java.Path, preferredPaths[index])) return index;
            return int.MaxValue;
        }
        return candidates.Where(java => java.MajorVersion >= minimum)
            .OrderBy(java => java.MajorVersion).ThenBy(Preference)
            .ThenByDescending(java => java.Arch == SystemInfo.SystemArch)
            .ThenBy(java => java.Path, comparer).FirstOrDefault();
    }

    private void UpdateLaunchEnvironment(string path, JavaInfoObj? actual, StringComparer comparer)
    {
        lock (_sync)
        {
            var remaining = _environments.Where(item => !comparer.Equals(
                JavaExecutablePreference.PreferWindowed(item.Path), path));
            _environments = (actual is null ? remaining : remaining.Prepend(actual with { Path = path }))
                .OrderByDescending(item => item.MajorVersion).ThenBy(item => item.Version).ToArray();
        }
        Changed?.Invoke();
    }
}

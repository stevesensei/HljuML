namespace HljuML.Core.GamePlay;

/// <summary>Windows 同一运行环境优先使用无控制台的 javaw.exe；其他目录和系统保持原来的路径。</summary>
internal static class JavaExecutablePreference
{
    /// <summary>只在所选 java.exe 的同一目录查找 javaw.exe，不跨环境替换版本。</summary>
    internal static string PreferWindowed(string path)
    {
        if (!OperatingSystem.IsWindows() || !Path.IsPathFullyQualified(path)
            || !Path.GetFileName(path).Equals("java.exe", StringComparison.OrdinalIgnoreCase))
            return path;
        var windowed = Path.Combine(Path.GetDirectoryName(path)!, "javaw.exe");
        return File.Exists(windowed) ? windowed : path;
    }
}

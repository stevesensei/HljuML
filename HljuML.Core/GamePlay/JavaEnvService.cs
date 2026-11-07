using ColorMC.Core.Helpers;
using ColorMC.Core.LaunchPath;
using ColorMC.Core.Objs;
using ColorMC.Core.Utils;

namespace HljuML.Core.GamePlay;

public sealed class JavaEnvService
{
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

        var discovered = await JavaHelper.FindJava();
        cancellationToken.ThrowIfCancellationRequested();
        var candidates = (discovered ?? []).Concat(new[] { registered, fromEnvironment }).OfType<JavaInfoObj>();
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
}

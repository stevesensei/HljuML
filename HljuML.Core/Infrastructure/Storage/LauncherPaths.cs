using ColorMC.Core;

namespace HljuML.Core.Infrastructure.Storage;

public static class LauncherPaths
{
    public static string LauncherDirectory => Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    public static string ColorMcDirectory => Path.Combine(LauncherDirectory, "HljuML");

    public static bool IsLauncherDirectory(string path) => GameFolderPathsEqual(path, LauncherDirectory);

    internal static bool GameFolderPathsEqual(string first, string second) =>
        Path.IsPathFullyQualified(first) && Path.IsPathFullyQualified(second)
        && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public static string DatabasePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);
            if (!Path.IsPathFullyQualified(appData))
                throw new InvalidOperationException("Cannot resolve the local application data directory.");

            return Path.Combine(appData, "HljuML", "launcher.db");
        }
    }

    internal static string LegacyDatabasePath => Path.Combine(ColorMcDirectory, "launcher.db");

    internal static string DefaultInstanceDirectory =>
        Path.Combine(ColorMcDirectory, Names.NameMinecraftKey, Names.NameInstanceDir);
}

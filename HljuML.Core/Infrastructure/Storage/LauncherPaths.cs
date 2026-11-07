using ColorMC.Core;

namespace HljuML.Core.Infrastructure.Storage;

public static class LauncherPaths
{
    public static string ColorMcDirectory => Path.Combine(AppContext.BaseDirectory, "HljuML");

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

using ColorMC.Core;

namespace HljuML.Core.Infrastructure;

public static class LauncherInfo
{
    private const string LauncherName = "HljuML";
    private const string LauncherMajorVersion = "0";
    private const string LauncherMinorVersion = "1.0";
    private const string LauncherBuildNumber = "ceffb6";
    
    public static string Version => $"{LauncherMajorVersion}.{LauncherMinorVersion}-{LauncherBuildNumber}";

    public static string CoreFrameworkVersion => ColorMCCore.Version;
}
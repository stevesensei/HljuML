using ColorMC.Core.Objs;

namespace HljuML.Core.GamePlay;

public sealed record GameVersionInfo(string Id, string Type, DateTimeOffset? ReleasedAt)
{
    public string? MetadataUrl { get; init; }
    public string? Sha1 { get; init; }
    public bool IsLatestRelease { get; init; }
    public bool IsLatestSnapshot { get; init; }
}

public sealed record GameLoaderVersion(Loaders Loader, string Version, string? Label = null);

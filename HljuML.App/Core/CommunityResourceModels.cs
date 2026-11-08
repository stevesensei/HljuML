using System;

namespace HljuML.App.Core;

public enum DownloadSection
{
    Vanilla,
    Mods,
    Modpacks,
    DataPacks,
    ResourcePacks,
    Shaders
}

public enum CommunityResourceType
{
    Mod,
    Modpack,
    DataPack,
    ResourcePack,
    Shader
}

public sealed record CommunityTargetInstance(Guid Id, string DisplayName, string Version, string Loader)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed record CommunityResourceItem(
    string Id,
    string Name,
    string Summary,
    string Author,
    string VersionLabel,
    bool IsCompatible);
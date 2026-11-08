using System;
using System.ComponentModel;

namespace HljuML.App.Core;

public sealed class DownloadHubViewModel : INotifyPropertyChanged, IDisposable
{
    public DownloadHubViewModel(GameDownloadViewModel gameDownloads, CommunityResourceViewModel community)
    {
        GameDownloads = gameDownloads;
        Community = community;
        Community.VanillaCreationRequested += OnVanillaCreationRequested;
    }

    public GameDownloadViewModel GameDownloads { get; }
    public CommunityResourceViewModel Community { get; }
    public DownloadSection SelectedSection { get; private set; } = DownloadSection.Vanilla;

    public bool IsVanilla => SelectedSection == DownloadSection.Vanilla;
    public bool IsCommunity => !IsVanilla;
    public bool IsMods => SelectedSection == DownloadSection.Mods;
    public bool IsModpacks => SelectedSection == DownloadSection.Modpacks;
    public bool IsDataPacks => SelectedSection == DownloadSection.DataPacks;
    public bool IsResourcePacks => SelectedSection == DownloadSection.ResourcePacks;
    public bool IsShaders => SelectedSection == DownloadSection.Shaders;

    public void Dispose()
    {
        Community.VanillaCreationRequested -= OnVanillaCreationRequested;
        Community.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Select(DownloadSection section)
    {
        if (SelectedSection == section)
        {
            return;
        }

        SelectedSection = section;
        if (section != DownloadSection.Vanilla)
        {
            Community.ResourceType = section switch
            {
                DownloadSection.Mods => CommunityResourceType.Mod,
                DownloadSection.Modpacks => CommunityResourceType.Modpack,
                DownloadSection.DataPacks => CommunityResourceType.DataPack,
                DownloadSection.ResourcePacks => CommunityResourceType.ResourcePack,
                DownloadSection.Shaders => CommunityResourceType.Shader,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        foreach (var name in new[]
                 {
                     nameof(SelectedSection), nameof(IsVanilla), nameof(IsCommunity),
                     nameof(IsMods), nameof(IsModpacks), nameof(IsDataPacks), nameof(IsResourcePacks), nameof(IsShaders)
                 })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void OnVanillaCreationRequested()
    {
        GameDownloads.BackToCatalog();
        Select(DownloadSection.Vanilla);
    }
}
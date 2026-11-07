using System;
using System.ComponentModel;

namespace HljuML.App.Core;

public sealed class DownloadHubViewModel : INotifyPropertyChanged, IDisposable
{
    private DownloadSection _selectedSection = DownloadSection.Vanilla;

    public DownloadHubViewModel(GameDownloadViewModel gameDownloads, CommunityResourceViewModel community)
    {
        GameDownloads = gameDownloads;
        Community = community;
        Community.VanillaCreationRequested += OnVanillaCreationRequested;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public GameDownloadViewModel GameDownloads { get; }
    public CommunityResourceViewModel Community { get; }
    public DownloadSection SelectedSection => _selectedSection;
    public bool IsVanilla => _selectedSection == DownloadSection.Vanilla;
    public bool IsCommunity => !IsVanilla;
    public bool IsMods => _selectedSection == DownloadSection.Mods;
    public bool IsModpacks => _selectedSection == DownloadSection.Modpacks;
    public bool IsDataPacks => _selectedSection == DownloadSection.DataPacks;
    public bool IsResourcePacks => _selectedSection == DownloadSection.ResourcePacks;
    public bool IsShaders => _selectedSection == DownloadSection.Shaders;

    public void Select(DownloadSection section)
    {
        if (_selectedSection == section) return;
        _selectedSection = section;
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

        foreach (var name in new[] { nameof(SelectedSection), nameof(IsVanilla), nameof(IsCommunity),
            nameof(IsMods), nameof(IsModpacks), nameof(IsDataPacks), nameof(IsResourcePacks), nameof(IsShaders) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void OnVanillaCreationRequested()
    {
        GameDownloads.BackToCatalog();
        Select(DownloadSection.Vanilla);
    }

    public void Dispose()
    {
        Community.VanillaCreationRequested -= OnVanillaCreationRequested;
        Community.Dispose();
    }
}

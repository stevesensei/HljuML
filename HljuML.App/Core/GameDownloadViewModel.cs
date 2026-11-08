using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using HljuML.Core.GamePlay;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Shared.Model;

namespace HljuML.App.Core;

public sealed partial class GameDownloadViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly GameCatalogService _catalog;
    private readonly GameInstallationService _installer;
    private readonly CancellationToken _stopping;
    private string _catalogFilter = "release";
    private bool _disposed;
    private bool _initialized;
    private string _searchText = "";
    private IReadOnlyList<GameVersionInfo> _versions = [];

    public GameDownloadViewModel(GameCatalogService catalog, GameInstallationService installer,
        CancellationToken stopping)
    {
        _catalog = catalog;
        _installer = installer;
        _stopping = stopping;
    }

    public bool IsCatalogLoading { get; private set; }
    public string? CatalogError { get; private set; }
    public bool HasCatalogError => CatalogError is not null;
    public bool HasVersions => _versions.Count > 0;
    public bool IsCatalogEmpty => !IsCatalogLoading && !HasCatalogError && !HasVersions;
    public IReadOnlyList<GameCatalogRow> CatalogRows { get; private set; } = [];
    public bool HasCatalogRows => CatalogRows.Count > 0;
    public bool IsCatalogFilteredEmpty => HasVersions && !IsCatalogLoading && CatalogRows.Count == 0;
    public GameCatalogRow? LatestRelease { get; private set; }
    public GameCatalogRow? LatestSnapshot { get; private set; }
    public bool HasLatestRelease => LatestRelease is not null;
    public bool HasLatestSnapshot => LatestSnapshot is not null;
    public bool IsConfiguring => SelectedVersion is not null;
    public bool IsBrowsing => !IsConfiguring;
    public GameVersionInfo? SelectedVersion { get; private set; }
    public string SelectedVersionLabel => $"Minecraft {SelectedVersion?.Id}";
    public string TargetDirectory => LauncherPaths.DefaultInstanceDirectory;

    public string CatalogFilter
    {
        get => _catalogFilter;
        set
        {
            if (value is not ("release" or "snapshot" or "old") || value == _catalogFilter)
            {
                return;
            }

            _catalogFilter = value;
            RebuildRows();
            Notify(nameof(CatalogFilter));
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            value ??= "";
            if (value == _searchText)
            {
                return;
            }

            _searchText = value;
            RebuildRows();
            Notify(nameof(SearchText));
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _loaderRequest?.Cancel();
        _loaderRequest?.Dispose();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Task InitializeAsync()
    {
        if (_initialized || _disposed)
        {
            return Task.CompletedTask;
        }

        _initialized = true;
        return RefreshCatalogAsync();
    }

    public async Task RefreshCatalogAsync()
    {
        if (IsCatalogLoading || _disposed)
        {
            return;
        }

        IsCatalogLoading = true;
        CatalogError = null;
        NotifyCatalog();
        try
        {
            var result = await _catalog.GetVersionsAsync(HasVersions, _stopping);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed)
                {
                    return;
                }

                CatalogError = result.Status == ServiceStatus.Success ? null : result.Message;
                if (result.Data is { } versions)
                {
                    _versions = versions;
                    LatestRelease = ToRow(versions.FirstOrDefault(version => version.IsLatestRelease)
                        ?? versions.FirstOrDefault(version => version.Type == "release"), null);
                    LatestSnapshot = ToRow(versions.FirstOrDefault(version => version.IsLatestSnapshot)
                        ?? versions.FirstOrDefault(version => version.Type == "snapshot"), null);
                    RebuildRows();
                }
            });
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsCatalogLoading = false;
                if (!_disposed)
                {
                    NotifyCatalog();
                }
            });
        }
    }

    public void ToggleGroup(string group)
    {
        CatalogFilter = group;
    }

    private void RebuildRows()
    {
        CatalogRows = _versions
            .Where(version => _catalogFilter == "old"
                ? version.Type is not ("release" or "snapshot")
                : version.Type == _catalogFilter)
            .Where(version => string.IsNullOrWhiteSpace(_searchText)
                || version.Id.Contains(_searchText.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(version => ToRow(version, null)!)
            .ToArray();
        Notify(nameof(CatalogRows));
        Notify(nameof(HasCatalogRows));
        Notify(nameof(IsCatalogFilteredEmpty));
    }

    private static GameCatalogRow? ToRow(GameVersionInfo? version, string? prefix)
    {
        return version is null
            ? null
            : new GameCatalogRow(version, "", version.Id,
                string.Join(" · ", new[] { prefix, version.ReleasedAt?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") }
                    .Where(part => !string.IsNullOrEmpty(part))));
    }

    private void NotifyCatalog()
    {
        foreach (var name in new[]
                 {
                     nameof(IsCatalogLoading), nameof(CatalogError), nameof(HasCatalogError),
                     nameof(HasVersions), nameof(IsCatalogEmpty), nameof(LatestRelease), nameof(LatestSnapshot),
                     nameof(HasLatestRelease), nameof(HasLatestSnapshot), nameof(HasCatalogRows),
                     nameof(IsCatalogFilteredEmpty)
                 }) Notify(name);
    }

    private void Notify(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record GameCatalogRow(GameVersionInfo? Version, string Group, string Title, string Detail)
{
    public bool IsHeader => Version is null;
    public bool IsVersion => Version is not null;
}
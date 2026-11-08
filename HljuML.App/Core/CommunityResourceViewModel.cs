using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using HljuML.Core.GamePlay;

namespace HljuML.App.Core;

public sealed partial class CommunityResourceViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly IReadOnlyList<string> LoaderFilters =
        ["全部加载器", "原版", "Fabric", "Forge", "NeoForge", "Quilt"];
    private static readonly IReadOnlyList<string> SortFilters = ["相关度", "最近更新", "最多下载"];
    private readonly GameInstanceService _gameInstances;
    private readonly CancellationToken _stopping;
    private bool _initialized;
    private bool _disposed;
    private string _searchText = string.Empty;
    private string _selectedVersion = "全部版本";
    private string _selectedLoader = LoaderFilters[0];
    private string _selectedCategory = "全部分类";
    private string _selectedSort = SortFilters[0];
    private CommunityTargetInstance? _selectedTarget;
    private CommunityResourceItem? _selectedResource;
    private CommunityResourceType _resourceType = CommunityResourceType.Mod;

    public CommunityResourceViewModel(GameInstanceService gameInstances, ModpackImportService imports, CancellationToken stopping)
    {
        _gameInstances = gameInstances;
        _imports = imports;
        _stopping = stopping;
        CategoryOptions = CategoriesFor(_resourceType);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? VanillaCreationRequested;

    public CommunityResourceType ResourceType
    {
        get => _resourceType;
        set
        {
            if (_resourceType == value) return;
            _resourceType = value;
            CategoryOptions = CategoriesFor(value);
            _selectedCategory = CategoryOptions[0];
            _selectedResource = null;
            NotifyPage();
        }
    }

    public string Title => ResourceType switch
    {
        CommunityResourceType.Mod => "Mod",
        CommunityResourceType.Modpack => "整合包",
        CommunityResourceType.DataPack => "数据包",
        CommunityResourceType.ResourcePack => "资源包",
        CommunityResourceType.Shader => "光影包",
        _ => throw new ArgumentOutOfRangeException()
    };

    public string Description => ResourceType switch
    {
        CommunityResourceType.Mod => "查找适合当前游戏版本与加载器的 Mod。",
        CommunityResourceType.Modpack => "整合包会创建新实例，并使用默认实例目录。",
        CommunityResourceType.DataPack => "数据包需要选择目标世界；世界读取将在资源提供方接入后开放。",
        CommunityResourceType.ResourcePack => "查找可安装到兼容实例的资源包。",
        CommunityResourceType.Shader => "查找适合兼容加载器与游戏版本的光影包。",
        _ => throw new ArgumentOutOfRangeException()
    };

    public bool RequiresTargetInstance => ResourceType != CommunityResourceType.Modpack;
    public bool ShowsModpackDestination => ResourceType == CommunityResourceType.Modpack;
    public bool ShowsWorldNotice => ResourceType == CommunityResourceType.DataPack;
    public string ModpackDestination => HljuML.Core.Infrastructure.Storage.LauncherPaths.DefaultInstanceDirectory;
    public IReadOnlyList<CommunityTargetInstance> TargetInstances { get; private set; } = [];
    public IReadOnlyList<string> VersionOptions { get; private set; } = ["全部版本"];
    public IReadOnlyList<string> LoaderOptions => LoaderFilters;
    public IReadOnlyList<string> CategoryOptions { get; private set; }
    public IReadOnlyList<string> SortOptions => SortFilters;
    public IReadOnlyList<CommunityResourceItem> Results { get; private set; } = [];
    public bool HasResults => Results.Count > 0;
    public bool IsLoadingTargets { get; private set; }
    public string? TargetLoadError { get; private set; }
    public bool HasTargetLoadError => TargetLoadError is not null;
    public bool HasSelectedResource => SelectedResource is not null;
    public bool IsBrowsingResults => SelectedResource is null;
    public bool CanInstall => false;

    public string SearchText { get => _searchText; set => Set(ref _searchText, value, nameof(SearchText)); }
    public string SelectedVersion { get => _selectedVersion; set => Set(ref _selectedVersion, value, nameof(SelectedVersion)); }
    public string SelectedLoader { get => _selectedLoader; set => Set(ref _selectedLoader, value, nameof(SelectedLoader)); }
    public string SelectedCategory { get => _selectedCategory; set => Set(ref _selectedCategory, value, nameof(SelectedCategory)); }
    public string SelectedSort { get => _selectedSort; set => Set(ref _selectedSort, value, nameof(SelectedSort)); }
    public CommunityTargetInstance? SelectedTarget { get => _selectedTarget; set => Set(ref _selectedTarget, value, nameof(SelectedTarget)); }
    public CommunityResourceItem? SelectedResource
    {
        get => _selectedResource;
        set
        {
            if (!Set(ref _selectedResource, value, nameof(SelectedResource))) return;
            Notify(nameof(HasSelectedResource));
            Notify(nameof(IsBrowsingResults));
            Notify(nameof(CanInstall));
        }
    }

    public Task InitializeAsync()
    {
        if (_initialized || _disposed) return Task.CompletedTask;
        _initialized = true;
        return LoadTargetsAsync();
    }

    public async Task LoadTargetsAsync()
    {
        var shouldLoad = false;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_disposed || IsLoadingTargets) return;
            IsLoadingTargets = true;
            TargetLoadError = null;
            NotifyTargetState();
            shouldLoad = true;
        });
        if (!shouldLoad) return;
        try
        {
            var instances = await _gameInstances.GetGameInstances(_stopping);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed) return;
                TargetInstances = instances.Select(instance => new CommunityTargetInstance(instance.Id,
                    $"{instance.Name} · {instance.Version}", instance.Version, instance.Loader.ToString())).ToArray();
                VersionOptions = new[] { "全部版本" }.Concat(instances.Select(instance => instance.Version)
                    .Where(version => !string.IsNullOrWhiteSpace(version)).Distinct().Order()).ToArray();
                SelectedTarget = TargetInstances.FirstOrDefault(target => target.Id == SelectedTarget?.Id)
                    ?? TargetInstances.FirstOrDefault();
                Notify(nameof(TargetInstances));
                Notify(nameof(VersionOptions));
            });
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (DbException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_disposed) TargetLoadError = "无法读取兼容实例，请稍后重试。";
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoadingTargets = false;
                if (!_disposed) NotifyTargetState();
            });
        }
    }

    public void RequestVanillaCreation() => VanillaCreationRequested?.Invoke();
    public void ClearSelection() => SelectedResource = null;

    private static IReadOnlyList<string> CategoriesFor(CommunityResourceType type) => type switch
    {
        CommunityResourceType.Mod => ["全部分类", "冒险", "科技", "魔法", "装饰", "性能"],
        CommunityResourceType.Modpack => ["全部分类", "冒险", "科技", "魔法", "轻量", "多人"],
        CommunityResourceType.DataPack => ["全部分类", "玩法", "世界生成", "实用", "装饰"],
        CommunityResourceType.ResourcePack => ["全部分类", "写实", "风格化", "简洁", "界面"],
        CommunityResourceType.Shader => ["全部分类", "写实", "性能", "风格化"],
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private void NotifyPage()
    {
        foreach (var name in new[] { nameof(ResourceType), nameof(Title), nameof(Description),
            nameof(RequiresTargetInstance), nameof(ShowsModpackDestination), nameof(ShowsWorldNotice),
            nameof(CategoryOptions), nameof(SelectedCategory), nameof(SelectedResource),
            nameof(HasSelectedResource), nameof(IsBrowsingResults), nameof(CanInstall) }) Notify(name);
    }

    private void NotifyTargetState()
    {
        Notify(nameof(IsLoadingTargets));
        Notify(nameof(TargetLoadError));
        Notify(nameof(HasTargetLoadError));
    }

    private bool Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name);
        return true;
    }

    private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public void Dispose() => _disposed = true;
}

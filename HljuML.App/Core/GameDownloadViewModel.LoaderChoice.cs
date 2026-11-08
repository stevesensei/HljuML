using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Media.Imaging;
using ColorMC.Core.Objs;
using HljuML.Core.GamePlay;

namespace HljuML.App.Core;

public sealed class GameLoaderChoice(Loaders loader) : INotifyPropertyChanged
{
    private bool _isExpanded;

    public event PropertyChangedEventHandler? PropertyChanged;
    public Loaders Loader { get; } = loader;
    public string Label => Loader.ToString();
    public Bitmap Icon => GameLoaderIcons.Get(Loader);
    public IReadOnlyList<GameLoaderVersion> Versions { get; private set; } = [];
    public bool IsLoading { get; private set; } = true;
    public string? Error { get; private set; }
    public bool IsDisabled { get; private set; } = true;
    public bool IsSelected { get; private set; }
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }
    public string Description { get; private set; } = "正在获取可用版本…";

    internal void Reset()
    {
        IsLoading = true;
        Error = null;
        Versions = [];
        IsExpanded = false;
        NotifyAll();
    }

    internal void Complete(IReadOnlyList<GameLoaderVersion> versions, string? error)
    {
        Versions = versions;
        Error = error;
        IsLoading = false;
        NotifyAll();
    }

    internal void Update(Loaders selected)
    {
        var incompatible = selected != Loaders.Normal && selected != Loader;
        IsDisabled = incompatible || IsLoading || Error is not null || Versions.Count == 0;
        IsSelected = selected == Loader;
        if (!IsSelected) IsExpanded = false;
        Description = incompatible ? $"与 {selected} 不兼容"
            : IsLoading ? "正在获取可用版本…"
            : Error is not null ? "获取失败，请重试"
            : Versions.Count == 0 ? "此游戏版本暂无可用版本"
            : selected == Loader ? "已选择，版本可在下方更改" : $"可以添加 · {Versions.Count} 个版本";
        NotifyAll();
    }

    private void NotifyAll()
    {
        foreach (var property in new[] { nameof(Versions), nameof(IsLoading), nameof(Error), nameof(IsDisabled),
            nameof(IsSelected), nameof(Description) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}

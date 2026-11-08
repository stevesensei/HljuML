using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ColorMC.Core.Objs;
using HljuML.Core.GamePlay;
using HljuML.Shared.Model;
using TKCode.Arc;

namespace HljuML.App.Core;

public sealed partial class GameDownloadViewModel
{
    private string _instanceName = "";
    private CancellationTokenSource? _loaderRequest;
    private bool _nameEdited;
    private Loaders _selectedLoader = Loaders.Normal;
    private SelectOption? _selectedLoaderVersion;
    private int _selectionGeneration;

    public GameLoaderChoice Forge { get; } = new(Loaders.Forge);
    public GameLoaderChoice NeoForge { get; } = new(Loaders.NeoForge);
    public GameLoaderChoice Fabric { get; } = new(Loaders.Fabric);
    public GameLoaderChoice OptiFine { get; } = new(Loaders.OptiFine);
    private GameLoaderChoice[] Choices => [Forge, NeoForge, Fabric, OptiFine];
    public IReadOnlyList<GameLoaderChoice> LoaderChoices => Choices;

    public IReadOnlyList<RadioCardOption> LoaderOptions { get; } =
    [
        new(Loaders.Forge, "Forge", "正在获取可用版本…"),
        new(Loaders.NeoForge, "NeoForge", "正在获取可用版本…"),
        new(Loaders.Fabric, "Fabric", "正在获取可用版本…"),
        new(Loaders.OptiFine, "OptiFine", "正在获取可用版本…")
    ];

    public bool IsSubmitting { get; private set; }
    public string? InstallationError { get; private set; }
    public bool HasInstallationError => InstallationError is not null;
    public bool IsEnqueued { get; private set; }

    public bool CanEditConfiguration => !IsSubmitting && !IsEnqueued;
    public bool HasLoader => _selectedLoader != Loaders.Normal;
    public bool HasLoaderErrors => Choices.Any(choice => choice.Error is not null);
    public bool AreLoadersLoading => Choices.Any(choice => choice.IsLoading);
    public string LoaderVersionLabel => $"{_selectedLoader} 版本";

    public string InstallationSummary => HasLoader
        ? $"Minecraft {SelectedVersion?.Id} · {_selectedLoader} {_selectedLoaderVersion?.Label}"
        : $"Minecraft {SelectedVersion?.Id} · 原版";

    public IReadOnlyList<SelectOption> LoaderVersions { get; private set; } = [];
    public Bitmap InstanceIcon => GameLoaderIcons.Get(_selectedLoader);

    public string? NameError => string.IsNullOrWhiteSpace(_instanceName) ? "请输入实例名称。"
        : _instanceName.Length > 128 || _instanceName.Any(char.IsControl) ? "名称最多 128 个字符，不能包含控制字符。" : null;

    public bool CanInstall => CanEditConfiguration && SelectedVersion is not null && NameError is null
        && (!HasLoader || _selectedLoaderVersion is not null);

    public string InstanceName
    {
        get => _instanceName;
        set
        {
            if (_instanceName == value)
            {
                return;
            }

            _instanceName = value;
            _nameEdited = true;
            InstallationError = null;
            NotifyConfiguration();
        }
    }

    public object? SelectedLoader
    {
        get => HasLoader ? _selectedLoader : null;
        set
        {
            if (value is not Loaders loader || loader == _selectedLoader || !CanEditConfiguration)
            {
                return;
            }

            var choice = Choices.FirstOrDefault(item => item.Loader == loader);
            if (choice is null || choice.IsDisabled)
            {
                return;
            }

            _selectedLoader = loader;
            LoaderVersions = choice.Versions.Select(item => new SelectOption(item.Version, item.Label ?? item.Version))
                .ToArray();
            _selectedLoaderVersion = LoaderVersions.FirstOrDefault();
            RegenerateName();
            UpdateLoaderStates();
            NotifyConfiguration();
        }
    }

    public SelectOption? SelectedLoaderVersion
    {
        get => _selectedLoaderVersion;
        set
        {
            if (value is null || Equals(value, _selectedLoaderVersion) || !CanEditConfiguration)
            {
                return;
            }

            _selectedLoaderVersion = value;
            RegenerateName();
            NotifyConfiguration();
        }
    }

    public void SelectLoader(GameLoaderChoice choice)
    {
        if (choice.IsSelected)
        {
            RemoveLoader();
            return;
        }

        SelectedLoader = choice.Loader;
    }

    public async Task SelectVersionAsync(GameVersionInfo version)
    {
        if (IsSubmitting)
        {
            return;
        }

        _loaderRequest?.Cancel();
        _loaderRequest?.Dispose();
        _loaderRequest = CancellationTokenSource.CreateLinkedTokenSource(_stopping);
        var generation = ++_selectionGeneration;
        SelectedVersion = version;
        _selectedLoader = Loaders.Normal;
        _selectedLoaderVersion = null;
        LoaderVersions = [];
        _nameEdited = false;
        IsEnqueued = false;
        InstallationError = null;
        RegenerateName();
        foreach (var choice in Choices) choice.Reset();
        UpdateLoaderStates();
        NotifyConfiguration();
        await Task.WhenAll(Choices.Select(choice =>
            LoadChoiceAsync(choice, version.Id, generation, _loaderRequest.Token)));
    }

    public async Task RetryLoadersAsync()
    {
        if (SelectedVersion is not { } version || _loaderRequest is null)
        {
            return;
        }

        var failed = Choices.Where(choice => choice.Error is not null).ToArray();
        foreach (var choice in failed) choice.Reset();
        UpdateLoaderStates();
        NotifyConfiguration();
        await Task.WhenAll(failed.Select(choice =>
            LoadChoiceAsync(choice, version.Id, _selectionGeneration, _loaderRequest.Token)));
    }

    private async Task LoadChoiceAsync(GameLoaderChoice choice, string version, int generation, CancellationToken token)
    {
        try
        {
            var result = await _catalog.GetLoaderVersionsAsync(version, choice.Loader, token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed || generation != _selectionGeneration)
                {
                    return;
                }

                choice.Complete(result.Status == ServiceStatus.Success ? result.Data ?? [] : [],
                    result.Status == ServiceStatus.Success ? null : result.Message);
                UpdateLoaderStates();
                NotifyConfiguration();
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    public void RemoveLoader()
    {
        if (!CanEditConfiguration)
        {
            return;
        }

        _selectedLoader = Loaders.Normal;
        _selectedLoaderVersion = null;
        LoaderVersions = [];
        RegenerateName();
        UpdateLoaderStates();
        NotifyConfiguration();
    }

    public void BackToCatalog()
    {
        if (IsSubmitting)
        {
            return;
        }

        _loaderRequest?.Cancel();
        ++_selectionGeneration;
        SelectedVersion = null;
        NotifyConfiguration();
    }

    public async Task InstallAsync()
    {
        if (!CanInstall || SelectedVersion is not { } version)
        {
            return;
        }

        IsSubmitting = true;
        InstallationError = null;
        NotifyConfiguration();
        try
        {
            var result = await _installer.EnqueueAsync(new GameInstallationRequest(_instanceName.Trim(), version.Id,
                _selectedLoader, _selectedLoaderVersion?.Value as string), _stopping);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed)
                {
                    return;
                }

                IsEnqueued = result.Status == ServiceStatus.Success;
                InstallationError = IsEnqueued ? null : result.Message;
            });
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSubmitting = false;
                if (!_disposed)
                {
                    NotifyConfiguration();
                }
            });
        }
    }

    private void RegenerateName()
    {
        if (!_nameEdited)
        {
            _instanceName = SelectedVersion?.Id
                + (HasLoader ? $"-{_selectedLoader}_{_selectedLoaderVersion?.Value}" : "");
        }

        InstallationError = null;
    }

    private void UpdateLoaderStates()
    {
        foreach (var choice in Choices) choice.Update(_selectedLoader);
        foreach (var option in LoaderOptions)
        {
            if (option.Value is not Loaders loader)
            {
                continue;
            }

            var choice = Choices.First(item => item.Loader == loader);
            option.Description = choice.Description;
            option.IsDisabled = choice.IsDisabled;
            option.DisabledReason = choice.Description;
        }
    }

    private void NotifyConfiguration()
    {
        foreach (var property in new[]
                 {
                     nameof(IsConfiguring), nameof(IsBrowsing), nameof(SelectedVersionLabel),
                     nameof(InstanceName), nameof(NameError), nameof(InstanceIcon), nameof(SelectedLoader),
                     nameof(HasLoader),
                     nameof(LoaderVersions), nameof(SelectedLoaderVersion), nameof(LoaderVersionLabel),
                     nameof(CanInstall),
                     nameof(InstallationSummary),
                     nameof(IsSubmitting), nameof(InstallationError), nameof(HasInstallationError), nameof(IsEnqueued),
                     nameof(CanEditConfiguration), nameof(HasLoaderErrors), nameof(AreLoadersLoading)
                 }) Notify(property);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ColorMC.Core.Objs;
using HljuML.App.Core;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Model.Entity;

namespace HljuML.App.Views;

public partial class InstancePickerWindow : Window
{
    private readonly MainViewModel? _viewModel;

    public InstancePickerWindow() => InitializeComponent();

    internal InstancePickerWindow(MainViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        SetCatalog(viewModel);
    }

    private void SetCatalog(MainViewModel viewModel, Guid? folderId = null)
    {
        if (viewModel.LaunchOptionsError is { } error)
        {
            EmptyInstances.Title = "无法读取实例";
            EmptyInstances.Description = error;
            return;
        }

        EmptyInstances.Title = "暂无游戏实例";
        EmptyInstances.Description = null;
        var rows = viewModel.AvailableInstances
            .Select(instance => new InstancePickerRow(instance, LoaderName(instance.Loader))).ToArray();
        var groups = new List<InstancePickerGroup> { new("全部实例", null, null, rows) };
        var rowsByFolder = rows.ToLookup(row => row.Instance.GameFolderId);
        groups.AddRange(viewModel.AvailableFolders.Select(folder =>
            new InstancePickerGroup(folder.Name, folder.AbsolutePath, folder.Id, rowsByFolder[folder.Id].ToArray())));
        var unassigned = rowsByFolder[null].ToArray();
        if (unassigned.Length > 0)
            groups.Add(new InstancePickerGroup("未分配目录", null, null, unassigned));

        var currentFolder = viewModel.AvailableFolders.FirstOrDefault(folder => LauncherPaths.IsLauncherDirectory(folder.AbsolutePath));
        CurrentFolderButton.Content = currentFolder is null ? "添加当前目录" : "扫描当前目录";
        var selected = rows.FirstOrDefault(row => row.Instance.Id == viewModel.SelectedInstanceId);
        var preferredFolder = folderId ?? selected?.Instance.GameFolderId ?? currentFolder?.Id;
        var group = groups.FirstOrDefault(item => item.FolderId == preferredFolder) ?? groups[0];
        GroupsList.ItemsSource = groups;
        GroupsList.SelectedItem = group;
        InstancesList.SelectedItem = group.Instances.FirstOrDefault(row => row == selected);
    }

    private async void OnAddCurrentFolderClicked(object? sender, RoutedEventArgs e) =>
        await AddFolderAsync(LauncherPaths.LauncherDirectory);

    private async void OnBrowseFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择游戏实例目录",
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(LauncherPaths.LauncherDirectory)
            });
            if (folders.FirstOrDefault() is { } folder)
            {
                using (folder)
                {
                    if (folder.TryGetLocalPath() is { } path)
                        await AddFolderAsync(path);
                    else
                        ShowFolderError("请选择本地文件夹。");
                }
            }
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowFolderError("无法打开文件夹选择器。");
        }
    }

    private async Task AddFolderAsync(string path)
    {
        if (_viewModel is not { } viewModel || !IsVisible || !FolderActions.IsEnabled)
            return;

        FolderActions.IsEnabled = false;
        FolderError.IsVisible = false;
        try
        {
            var result = await viewModel.AddGameFolderAsync(path);
            if (!IsVisible)
                return;
            if (result.Data is not { } folder)
            {
                ShowFolderError(result.Message);
                return;
            }

            await viewModel.RefreshLaunchOptionsAsync();
            if (IsVisible)
                SetCatalog(viewModel, folder.Id);
        }
        catch (OperationCanceledException) { }
        finally
        {
            FolderActions.IsEnabled = true;
        }
    }

    private void ShowFolderError(string message)
    {
        FolderError.Text = message;
        FolderError.IsVisible = true;
    }

    private void OnGroupChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (GroupsList.SelectedItem is not InstancePickerGroup group)
            return;

        var selection = InstancesList.SelectedItem;
        GroupTitle.Text = group.Name;
        InstancesList.ItemsSource = group.Instances;
        InstancesList.SelectedItem = group.Instances.FirstOrDefault(row => ReferenceEquals(row, selection));
        InstancesList.IsVisible = group.Count > 0;
        EmptyInstances.IsVisible = group.Count == 0;
    }

    private void OnInstanceChanged(object? sender, SelectionChangedEventArgs e) =>
        UseInstanceButton.IsEnabled = InstancesList.SelectedItem is InstancePickerRow;

    private void OnUseInstanceClicked(object? sender, RoutedEventArgs e)
    {
        if (InstancesList.SelectedItem is InstancePickerRow row)
            Close(row.Instance);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private static string LoaderName(Loaders loader) => loader switch
    {
        Loaders.Normal => "原版",
        Loaders.Custom => "自定义",
        _ => loader.ToString()
    };
}

public sealed record InstancePickerGroup(string Name, string? AbsolutePath, Guid? FolderId,
    IReadOnlyList<InstancePickerRow> Instances)
{
    public int Count => Instances.Count;
    public bool HasPath => !string.IsNullOrWhiteSpace(AbsolutePath);
}

public sealed record InstancePickerRow(GameInstanceData Instance, string Loader)
{
    public string Description => string.IsNullOrWhiteSpace(Instance.Version) ? Loader : $"{Instance.Version} · {Loader}";
}

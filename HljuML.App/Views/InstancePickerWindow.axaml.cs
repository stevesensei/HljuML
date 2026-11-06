using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ColorMC.Core.Objs;
using HljuML.Core.Model.Entity;

namespace HljuML.App.Views;

public partial class InstancePickerWindow : Window
{
    public InstancePickerWindow() => InitializeComponent();

    internal InstancePickerWindow(IReadOnlyList<GameInstanceData> instances, Guid? selectedId, string? error) : this()
    {
        if (error is not null)
        {
            EmptyInstances.Title = "无法读取实例";
            EmptyInstances.Description = error;
            return;
        }

        var rows = instances.Select(instance => new InstancePickerRow(instance, LoaderName(instance.Loader))).ToArray();
        var groups = new List<InstancePickerGroup> { new("全部实例", rows) };
        groups.AddRange(rows.GroupBy(row => row.Instance.Loader).OrderBy(group => group.Key)
            .Select(group => new InstancePickerGroup(LoaderName(group.Key), group.ToArray())));

        GroupsList.ItemsSource = groups;
        GroupsList.SelectedIndex = 0;
        InstancesList.SelectedItem = rows.FirstOrDefault(row => row.Instance.Id == selectedId);
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

public sealed record InstancePickerGroup(string Name, IReadOnlyList<InstancePickerRow> Instances)
{
    public int Count => Instances.Count;
}

public sealed record InstancePickerRow(GameInstanceData Instance, string Loader)
{
    public string Description => string.IsNullOrWhiteSpace(Instance.Version) ? Loader : $"{Instance.Version} · {Loader}";
}

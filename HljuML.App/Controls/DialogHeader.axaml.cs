using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HljuML.App.Controls;

public partial class DialogHeader : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DialogHeader, string?>(nameof(Title));
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public event EventHandler<RoutedEventArgs>? CloseRequested;
    public DialogHeader() => InitializeComponent();
    private void OnCloseClicked(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, e);
}

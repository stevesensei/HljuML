using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace HljuML.App;

public class App : Application
{
    private readonly Func<MainWindow>? _createMainWindow;

    public App()
    {
    }

    internal App(Func<MainWindow> createMainWindow)
    {
        _createMainWindow = createMainWindow;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = _createMainWindow?.Invoke() ?? new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
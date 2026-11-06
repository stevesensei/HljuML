using Avalonia;
using System;
using HljuML.App.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TKCode.Arc;

namespace HljuML.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });
        builder.Services.AddCommonServices();

        using var host = builder.Build();
        try
        {
            host.StartAsync().GetAwaiter().GetResult();
            ConfigureAvalonia(AppBuilder.Configure(() => new App(
                () => host.Services.GetRequiredService<MainWindow>())))
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => ConfigureAvalonia(AppBuilder.Configure<App>());

    private static AppBuilder ConfigureAvalonia(AppBuilder builder)
        => builder
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .UseArc(arc => arc
                .UseArcControls()
                .WithTheme(theme => theme
                    .Appearance(ArcAppearance.Light)
                    .Accent(ArcAccentPreset.Blue)))
            .LogToTrace();
}

using Avalonia;
using System;
using System.Threading;
using Avalonia.Controls;
using HljuML.App.Core;
using HljuML.Core.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TKCode.Arc;
using HljuML.Core.Infrastructure.Settings;

namespace HljuML.App;

class Program
{
    private const string InstanceName = "HljuML.Launcher.1a770ff1-9d65-4644-86ec-c9fe2753942a";
    private static Mutex? _instanceHandle;

    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });
        var settings = new LauncherSettingsService();
        var logs = new LauncherFileLoggerProvider(settings);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(logs);
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddFilter((_, level) => level >= settings.Value.MinimumLogLevel);
        builder.Logging.AddProvider(logs);
        builder.Logging.AddDebug();
        builder.Services.AddCommonServices();

        using var host = builder.Build();
        if (!TryRegisterInstance())
            return;

        try
        {
            host.StartAsync().GetAwaiter().GetResult();
            ConfigureAvalonia(AppBuilder.Configure(() => new App(
                () => host.Services.GetRequiredService<MainWindow>())))
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
        }
        finally
        {
            host.StopAsync().GetAwaiter().GetResult();
        }
    }

    private static bool TryRegisterInstance()
    {
        if (_instanceHandle is not null)
            return false;

        var name = OperatingSystem.IsWindows() ? @"Global\" + InstanceName : InstanceName;
        try
        {
            var handle = new Mutex(initiallyOwned: false, name, out var created);
            if (!created)
            {
                handle.Dispose();
                return false;
            }

            // Keep the named object alive through host shutdown and until the process exits.
            _instanceHandle = handle;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => ConfigureAvalonia(AppBuilder.Configure<App>());

    private static AppBuilder ConfigureAvalonia(AppBuilder builder)
        => builder
            .UsePlatformDetect()
            .WithInterFont()
            .UseArc(arc => arc
                .UseArcControls()
                .WithTheme(theme => theme
                    .Appearance(ArcAppearance.Light)
                    .Accent(ArcAccentPreset.Blue)))
            .LogToTrace();
}

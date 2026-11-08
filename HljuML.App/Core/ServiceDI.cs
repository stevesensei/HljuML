using HljuML.Core.GamePlay;
using HljuML.Core.GamePlay.Auth;
using HljuML.Core.Infrastructure.Downloads;
using HljuML.Core.Infrastructure.Logging;
using HljuML.Core.Infrastructure.Settings;
using HljuML.Core.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HljuML.App.Core;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = LauncherPaths.DatabasePath
        }.ToString();
        collection.AddDbContext<HljuMLDbContext>(options => options.UseSqlite(connectionString));
        collection.AddScoped<AuthServerService>();
        collection.AddSingleton<LegacyFrameworkAccountCache>();
        collection.AddScoped<PlayerProfileService>();
        collection.AddHostedService<SqliteDatabaseInitializer>();

        collection.AddSingleton<JavaEnvService>();
        collection.AddSingleton<JavaRuntimeService>();
        collection.AddSingleton<OperationCoordinatorService>();
        collection.AddSingleton<GameCatalogService>();
        collection.AddSingleton<GameInstallationService>();
        collection.AddSingleton<ModpackImportService>();
        collection.AddSingleton<GameInstanceService>();
        collection.AddHostedService(services => services.GetRequiredService<GameInstanceService>());
        collection.AddHostedService(services => services.GetRequiredService<GameInstallationService>());
        collection.AddHostedService(services => services.GetRequiredService<JavaEnvService>());
        collection.AddSingleton<DownloadManagementService>();
        collection.AddSingleton<DownloadTrafficService>();
        collection.AddHostedService(services => services.GetRequiredService<DownloadManagementService>());
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton(services => new MainWindow(services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<DownloadManagementService>(),
            services.GetRequiredService<LauncherSettingsService>(),
            services.GetRequiredService<LauncherFileLoggerProvider>())
        {
            DataContext = services.GetRequiredService<MainViewModel>()
        });
    }
}
using HljuML.Core.GamePlay;
using HljuML.Core.Infrastructure.Storage;
using HljuML.Core.Infrastructure.Downloads;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HljuML.Core.GamePlay.Auth;

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
        collection.AddScoped<PlayerProfileService>();
        collection.AddHostedService<SqliteDatabaseInitializer>();

        collection.AddSingleton<JavaEnvService>();
        collection.AddSingleton<ColorMcOperationCoordinator>();
        collection.AddSingleton<GameCatalogService>();
        collection.AddSingleton<GameInstallationService>();
        collection.AddSingleton<ModpackImportService>();
        collection.AddSingleton<GameInstanceService>();
        collection.AddHostedService(services => services.GetRequiredService<GameInstanceService>());
        collection.AddHostedService(services => services.GetRequiredService<GameInstallationService>());
        collection.AddHostedService(services => services.GetRequiredService<JavaEnvService>());
        collection.AddSingleton<DownloadManagementService>();
        collection.AddHostedService(services => services.GetRequiredService<DownloadManagementService>());
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton(services => new MainWindow(services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<DownloadManagementService>())
        {
            DataContext = services.GetRequiredService<MainViewModel>()
        });
    }
}

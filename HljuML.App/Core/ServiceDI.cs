using HljuML.Core.GamePlay;
using HljuML.Core.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using HljuML.Core.GamePlay.Auth;

namespace HljuML.App.Core;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(AppContext.BaseDirectory, "HljuML", "launcher.db")
        }.ToString();
        collection.AddDbContext<HljuMLDbContext>(options => options.UseSqlite(connectionString));
        collection.AddScoped<AuthServerService>();
        collection.AddScoped<PlayerProfileService>();
        collection.AddHostedService<SqliteDatabaseInitializer>();

        collection.AddSingleton<GameInstanceService>();
        collection.AddHostedService(services => services.GetRequiredService<GameInstanceService>());
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton(services => new MainWindow(services.GetRequiredService<IServiceScopeFactory>())
        {
            DataContext = services.GetRequiredService<MainViewModel>()
        });
    }
}

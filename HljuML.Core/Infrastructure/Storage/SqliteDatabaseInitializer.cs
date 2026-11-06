using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HljuML.Core.Infrastructure.Storage;

public sealed class SqliteDatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<HljuMLDbContext>();

        var databasePath = context.Database.GetDbConnection().DataSource;
        if (Path.GetDirectoryName(databasePath) is { Length: > 0 } directory)
            Directory.CreateDirectory(directory);

        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using HljuML.Core.Model.Entity;
using Microsoft.Data.Sqlite;
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

        ImportLegacyDatabase(databasePath, cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        await AssignDefaultGameFolder(context, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void ImportLegacyDatabase(string databasePath, CancellationToken cancellationToken)
    {
        if (File.Exists(databasePath) || !File.Exists(LauncherPaths.LegacyDatabasePath))
            return;

        cancellationToken.ThrowIfCancellationRequested();
        var temporaryPath = $"{databasePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var source = CreateConnection(LauncherPaths.LegacyDatabasePath, SqliteOpenMode.ReadOnly))
            using (var destination = CreateConnection(temporaryPath, SqliteOpenMode.ReadWriteCreate))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, databasePath);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static SqliteConnection CreateConnection(string path, SqliteOpenMode mode) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false
        }.ToString());

    private static async Task AssignDefaultGameFolder(HljuMLDbContext context, CancellationToken cancellationToken)
    {
        var path = LauncherPaths.DefaultInstanceDirectory;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var folder = await context.GameFolders.FirstOrDefaultAsync(item => item.AbsolutePath == path, cancellationToken);
        if (folder is null)
        {
            folder = new GameFolder { Id = Guid.NewGuid(), AbsolutePath = path, Name = "默认实例目录" };
            context.GameFolders.Add(folder);
            await context.SaveChangesAsync(cancellationToken);
        }

        await context.GameInstances.Where(instance => instance.GameFolderId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(instance => instance.GameFolderId, folder.Id),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

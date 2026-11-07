using HljuML.Core.Model.Entity;
using Microsoft.EntityFrameworkCore;

namespace HljuML.Core.Infrastructure.Storage;

public sealed class HljuMLDbContext(DbContextOptions<HljuMLDbContext> options) : DbContext(options)
{
    public DbSet<GameInstanceData> GameInstances => Set<GameInstanceData>();
    public DbSet<GameFolder> GameFolders => Set<GameFolder>();
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<CustomAuthServer> CustomAuthServers => Set<CustomAuthServer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerProfile>()
            .HasOne(profile => profile.CustomAuthServer)
            .WithMany()
            .HasForeignKey(profile => profile.AuthServerId);
    }
}

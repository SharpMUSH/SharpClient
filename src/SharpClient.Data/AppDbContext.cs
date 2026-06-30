using Microsoft.EntityFrameworkCore;
using SharpClient.Core.Domain;
using SharpClient.Core.Platform;

namespace SharpClient.Data;

public sealed class AppDbContext : DbContext
{
    // Expose sets for tests and direct queries
    public DbSet<World> Worlds => Set<World>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<TriggerRule> TriggerRules => Set<TriggerRule>();
    public DbSet<AliasRule> AliasRules => Set<AliasRule>();

    /// <summary>Production constructor: derives the connection string from <see cref="IAppStorage"/>.</summary>
    public AppDbContext(IAppStorage storage)
        : base(BuildOptions(storage))
    {
    }

    /// <summary>Testing constructor: accepts pre-built options (e.g. in-memory database).</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    private static DbContextOptions<AppDbContext> BuildOptions(IAppStorage storage) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={storage.GetDatabasePath()}")
            .Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<World>(entity =>
        {
            entity.HasKey(w => w.Id);

            // World → Characters (1:many, cascade)
            entity.HasMany(w => w.Characters)
                  .WithOne()
                  .HasForeignKey(c => c.WorldId)
                  .OnDelete(DeleteBehavior.Cascade);

            // World → world-scoped TriggerRules (shadow FK "WorldId" on TriggerRule)
            entity.HasMany(w => w.Triggers)
                  .WithOne()
                  .HasForeignKey("WorldId")
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);

            // World → world-scoped AliasRules (shadow FK "WorldId" on AliasRule)
            entity.HasMany(w => w.Aliases)
                  .WithOne()
                  .HasForeignKey("WorldId")
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Character>(entity =>
        {
            entity.HasKey(c => c.Id);

            // Character → character-scoped TriggerRules (shadow FK "CharacterId" on TriggerRule)
            entity.HasMany(c => c.Triggers)
                  .WithOne()
                  .HasForeignKey("CharacterId")
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);

            // Character → character-scoped AliasRules (shadow FK "CharacterId" on AliasRule)
            entity.HasMany(c => c.Aliases)
                  .WithOne()
                  .HasForeignKey("CharacterId")
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TriggerRule>().HasKey(t => t.Id);
        modelBuilder.Entity<AliasRule>().HasKey(a => a.Id);
    }
}

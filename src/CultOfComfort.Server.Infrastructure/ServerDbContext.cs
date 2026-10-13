using Microsoft.EntityFrameworkCore;

namespace CultOfComfort.Server.Infrastructure;

internal sealed class PlayerRow
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? RecoveryCodeHash { get; set; }
    public string? GoogleSubject { get; set; }
}

/// <summary>A village is one document (the shared save format) plus its version, for the optimistic check.</summary>
internal sealed class VillageRow
{
    public Guid PlayerId { get; set; }
    public long Version { get; set; }
    public string Json { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ServerDbContext(DbContextOptions<ServerDbContext> options) : DbContext(options)
{
    public DbSet<PlayerRow> Players => Set<PlayerRow>();

    public DbSet<VillageRow> Villages => Set<VillageRow>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PlayerRow>(e =>
        {
            e.ToTable("players");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.TokenHash).IsUnique();
            e.HasIndex(p => p.RecoveryCodeHash).IsUnique();
            e.HasIndex(p => p.GoogleSubject).IsUnique();
            // SQLite has no DateTimeOffset type: store UTC ticks, which also sort correctly.
            e.Property(p => p.CreatedAtUtc).HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
        });
        model.Entity<VillageRow>(e =>
        {
            e.ToTable("villages");
            e.HasKey(v => v.PlayerId);
            e.Property(v => v.UpdatedAtUtc).HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
        });
    }
}

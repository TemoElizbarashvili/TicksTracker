using Microsoft.EntityFrameworkCore;

namespace ExeTicksTracker.Data;

public class UsageDbContext : DbContext
{
    public DbSet<AppUsageInterval> AppUsageIntervals => Set<AppUsageInterval>();
    public DbSet<AppUsageAggregate> AppUsageAggregates => Set<AppUsageAggregate>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TickTracker",
            "usage.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUsageInterval>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProcessName).HasMaxLength(128);
            entity.Property(x => x.StartUtc).IsRequired();
            entity.Property(x => x.EndUtc).IsRequired();
            entity.Property(x => x.ProcessUsingDate).IsRequired();
        });

        modelBuilder.Entity<AppUsageAggregate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProcessName).HasMaxLength(128);
            entity.Property(x => x.FirstSeenUtc).IsRequired();
            entity.Property(x => x.LastSeenUtc).IsRequired();
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(64);
            entity.Property(x => x.Value).HasMaxLength(256);
        });
    }
}

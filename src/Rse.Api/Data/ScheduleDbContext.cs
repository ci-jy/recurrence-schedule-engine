using Microsoft.EntityFrameworkCore;

namespace Rse.Api.Data;

public class ScheduleDbContext(DbContextOptions<ScheduleDbContext> options) : DbContext(options)
{
    public DbSet<SeriesEntity> Series => Set<SeriesEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var series = modelBuilder.Entity<SeriesEntity>();
        series.ToTable("series");
        series.HasIndex(s => s.Resource);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Seed.Infrastructure.Persistence;

public class SeedDbContextFactory : IDesignTimeDbContextFactory<SeedDbContext>
{
    public SeedDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=seed;Username=seed;Password=seed_dev_password";
        var options = new DbContextOptionsBuilder<SeedDbContext>()
            .UseNpgsql(conn).Options;
        return new SeedDbContext(options);
    }
}

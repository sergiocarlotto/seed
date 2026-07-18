using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Seed.Domain.Access;
using Seed.Domain.Audit;
using Seed.Domain.Companies;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Identity;

namespace Seed.Infrastructure.Persistence;

public class SeedDbContext(DbContextOptions<SeedDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserCompanyAccess> UserCompanyAccesses => Set<UserCompanyAccess>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.Property(o => o.Name).IsRequired().HasMaxLength(200);
            e.HasQueryFilter(o => o.DeletedAt == null);
        });

        builder.Entity<Company>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.OrganizationId);
            e.HasQueryFilter(c => c.DeletedAt == null);
        });

        builder.Entity<UserCompanyAccess>(e =>
        {
            e.HasIndex(a => new { a.UserId, a.CompanyId }).IsUnique();
            e.HasIndex(a => a.UserId);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.Property(a => a.Action).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        });
    }
}

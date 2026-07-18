using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Seed.Domain.Audit;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Identity;

namespace Seed.Infrastructure.Persistence;

public class SeedDbContext(DbContextOptions<SeedDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.Property(o => o.Name).IsRequired().HasMaxLength(200);
            e.HasQueryFilter(o => o.DeletedAt == null);
        });

        builder.Entity<OrganizationMembership>(e =>
        {
            e.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
            e.HasOne(m => m.Organization)
                .WithMany(o => o.Memberships)
                .HasForeignKey(m => m.OrganizationId);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.Property(a => a.Action).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        });
    }
}

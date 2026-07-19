using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Seed.Domain.Access;
using Seed.Domain.AccessControl;
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
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfilePermission> ProfilePermissions => Set<ProfilePermission>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

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

        builder.Entity<Permission>(e =>
        {
            e.HasKey(p => p.Key);
            e.Property(p => p.Key).HasMaxLength(100);
            e.Property(p => p.Module).IsRequired().HasMaxLength(100);
            e.Property(p => p.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(500);
        });

        builder.Entity<Profile>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(500);
            e.HasIndex(p => new { p.OrganizationId, p.Name }).IsUnique();
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        builder.Entity<ProfilePermission>(e =>
        {
            e.HasKey(pp => new { pp.ProfileId, pp.PermissionKey });
            e.HasOne<Profile>().WithMany()
                .HasForeignKey(pp => pp.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Permission>().WithMany()
                .HasForeignKey(pp => pp.PermissionKey).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserProfile>(e =>
        {
            e.HasKey(up => new { up.UserId, up.ProfileId });
            e.HasOne<Profile>().WithMany()
                .HasForeignKey(up => up.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(up => up.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

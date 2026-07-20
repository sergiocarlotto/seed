using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Bootstrap no boot: perfil "Administrador" (is_system) com todas as permissões
// ativas por organização, e o admin semeado como owner ligado ao perfil.
public class AccessControlBootstrapTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Demo_org_has_system_admin_profile_with_all_active_permissions()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var orgId = await factory.GetDemoOrganizationIdAsync();

        var admin = await db.Profiles
            .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IsSystem);
        Assert.NotNull(admin);
        Assert.Equal(AccessControlBootstrapper.AdminProfileName, admin!.Name);
        Assert.Equal(ProfileStatus.Active, admin.Status);

        var granted = await db.ProfilePermissions
            .Where(pp => pp.ProfileId == admin.Id)
            .Select(pp => pp.PermissionKey)
            .ToListAsync();
        var activeKeys = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .Select(p => p.Key)
            .ToListAsync();

        Assert.NotEmpty(activeKeys);
        Assert.Equal(activeKeys.OrderBy(k => k), granted.OrderBy(k => k));
    }

    [Fact]
    public async Task Seeded_admin_is_owner_and_linked_to_admin_profile()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var adminUser = await db.Users.FirstAsync(u => u.Email == ApiFactory.AdminEmail);
        Assert.True(adminUser.IsOwner);

        var adminProfile = await db.Profiles
            .FirstAsync(p => p.OrganizationId == adminUser.OrganizationId && p.IsSystem);
        var linked = await db.UserProfiles
            .AnyAsync(up => up.UserId == adminUser.Id && up.ProfileId == adminProfile.Id);
        Assert.True(linked);
    }

    [Fact]
    public async Task Member_does_not_become_owner_or_linked()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        var email = $"member-{Guid.NewGuid():N}@demo.local";
        await factory.CreateUserAsync(email, "Passw0rd!", orgId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        await AccessControlBootstrapper.SeedAsync(db, default);

        var member = await db.Users.FirstAsync(u => u.Email == email);
        Assert.False(member.IsOwner);

        var adminProfile = await db.Profiles
            .FirstAsync(p => p.OrganizationId == orgId && p.IsSystem);
        var linked = await db.UserProfiles
            .AnyAsync(up => up.UserId == member.Id && up.ProfileId == adminProfile.Id);
        Assert.False(linked);
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        await AccessControlBootstrapper.SeedAsync(db, default);
        await AccessControlBootstrapper.SeedAsync(db, default);

        var systemProfiles = await db.Profiles
            .Where(p => p.OrganizationId == orgId && p.IsSystem)
            .ToListAsync();
        Assert.Single(systemProfiles);
        var adminProfile = systemProfiles[0];

        var grantedCount = await db.ProfilePermissions
            .CountAsync(pp => pp.ProfileId == adminProfile.Id);
        var activeCount = await db.Permissions
            .CountAsync(p => p.Status == PermissionStatus.Active);
        Assert.Equal(activeCount, grantedCount);

        var adminUser = await db.Users.FirstAsync(u => u.Email == ApiFactory.AdminEmail);
        var linkedCount = await db.UserProfiles
            .CountAsync(up => up.UserId == adminUser.Id && up.ProfileId == adminProfile.Id);
        Assert.Equal(1, linkedCount);
    }
}

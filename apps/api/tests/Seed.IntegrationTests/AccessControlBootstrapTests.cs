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
}

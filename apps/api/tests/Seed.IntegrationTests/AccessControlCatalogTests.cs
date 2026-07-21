using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Catálogo de permissões: reconciliação no boot, trava de FK e ciclo
// obsolete/reativação do reconciliador.
public class AccessControlCatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Catalog_is_reconciled_on_startup()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var keys = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .Select(p => p.Key)
            .ToListAsync();

        Assert.Contains(AccessControlPermissions.ProfilesManage, keys);
        Assert.Contains(AccessControlPermissions.ProfilesAssign, keys);
        Assert.Contains(AccessControlPermissions.UsersManage, keys);
    }

    [Fact]
    public async Task Fk_rejects_unknown_permission_key()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var orgId = await factory.GetDemoOrganizationIdAsync();
        var now = DateTime.UtcNow;
        var profile = new Profile
        {
            OrganizationId = orgId, Name = "Perfil FK Teste",
            CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();

        db.ProfilePermissions.Add(new ProfilePermission
        {
            ProfileId = profile.Id, PermissionKey = "does.not.exist",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Reconciler_marks_missing_as_obsolete_and_reactivates()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var withTemp = new List<PermissionDefinition>(AccessControlPermissions.Definitions)
        {
            new("temp.key", "temp", "Temp", "Permissão temporária de teste"),
        };

        // 1) temp.key entra como Active.
        await PermissionCatalogReconciler.ReconcileAsync(db, withTemp, default);
        var temp = await db.Permissions.SingleAsync(p => p.Key == "temp.key");
        Assert.Equal(PermissionStatus.Active, temp.Status);

        // 2) some do catálogo → Obsolete.
        await PermissionCatalogReconciler.ReconcileAsync(db, AccessControlPermissions.Definitions, default);
        await db.Entry(temp).ReloadAsync();
        Assert.Equal(PermissionStatus.Obsolete, temp.Status);

        // 3) reaparece → volta a Active.
        await PermissionCatalogReconciler.ReconcileAsync(db, withTemp, default);
        await db.Entry(temp).ReloadAsync();
        Assert.Equal(PermissionStatus.Active, temp.Status);
    }
}

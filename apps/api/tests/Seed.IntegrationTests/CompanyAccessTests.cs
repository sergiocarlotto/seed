using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Application.Companies;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Concessão e revogação de acesso a empresa (companies.grant_access) e a regra
// de escopo concedível da ADR-0014.
public class CompanyAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Grant_access_permission_is_reconciled_into_catalog()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.Key == CompaniesPermissions.GrantAccess);
        Assert.NotNull(permission);
        Assert.Equal(PermissionStatus.Active, permission!.Status);
        Assert.Equal("organizations", permission.Module);

        // O perfil de sistema recebe toda permissão ativa no boot (top-up).
        var orgId = await factory.GetDemoOrganizationIdAsync();
        var systemProfileId = await db.Profiles
            .Where(p => p.OrganizationId == orgId && p.IsSystem).Select(p => p.Id).FirstAsync();
        Assert.True(await db.ProfilePermissions.AnyAsync(pp =>
            pp.ProfileId == systemProfileId && pp.PermissionKey == CompaniesPermissions.GrantAccess));
    }
}

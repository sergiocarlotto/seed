using Microsoft.EntityFrameworkCore;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

public class PermissionQuery(SeedDbContext db) : IPermissionQuery
{
    public async Task<IReadOnlyList<PermissionGroupDto>> ListActiveGroupedAsync(CancellationToken ct)
    {
        var perms = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .OrderBy(p => p.Module).ThenBy(p => p.DisplayName)
            .ToListAsync(ct);

        return perms
            .GroupBy(p => p.Module)
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.Select(p => new PermissionItemDto(p.Key, p.DisplayName, p.Description)).ToList()))
            .ToList();
    }
}

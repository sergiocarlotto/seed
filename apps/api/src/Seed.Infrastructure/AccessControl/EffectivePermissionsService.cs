using Microsoft.EntityFrameworkCore;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

public class EffectivePermissionsService(SeedDbContext db, ICurrentUser currentUser)
    : IEffectivePermissions
{
    public async Task<IReadOnlySet<string>> ForCurrentUserAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null) return new HashSet<string>();

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null) return new HashSet<string>();

        // Owner: bypass funcional total (todas as permissões ativas do catálogo).
        if (user.IsOwner)
        {
            var all = await db.Permissions
                .Where(p => p.Status == PermissionStatus.Active)
                .Select(p => p.Key)
                .ToListAsync(ct);
            return all.ToHashSet();
        }

        // União das permissões ativas dos perfis ATIVOS (o query filter de
        // soft-delete de Profile já exclui perfis deletados; o Status exclui
        // arquivados). Permissões obsoletas são ignoradas.
        var keys = await (
            from up in db.UserProfiles
            join pr in db.Profiles on up.ProfileId equals pr.Id
            join pp in db.ProfilePermissions on pr.Id equals pp.ProfileId
            join perm in db.Permissions on pp.PermissionKey equals perm.Key
            where up.UserId == userId.Value
                  && pr.Status == ProfileStatus.Active
                  && perm.Status == PermissionStatus.Active
            select perm.Key
        ).Distinct().ToListAsync(ct);

        return keys.ToHashSet();
    }

    public async Task<bool> HasAsync(string permissionKey, CancellationToken ct)
        => (await ForCurrentUserAsync(ct)).Contains(permissionKey);
}

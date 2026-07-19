using Microsoft.EntityFrameworkCore;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

public class EffectivePermissionsService(SeedDbContext db, ICurrentUser currentUser)
    : IEffectivePermissions
{
    // Memoização por request: o serviço é scoped (uma instância por request),
    // então o resultado pode ser reaproveitado entre chamadas do mesmo request
    // sem comprometer a revogação imediata (o cache morre com o request).
    private IReadOnlySet<string>? _cache;

    public async Task<IReadOnlySet<string>> ForCurrentUserAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        var userId = currentUser.UserId;
        if (userId is null) return _cache = new HashSet<string>();

        var isOwner = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => (bool?)u.IsOwner)
            .FirstOrDefaultAsync(ct);
        if (isOwner is null) return _cache = new HashSet<string>();

        // Owner: bypass funcional total (todas as permissões ativas do catálogo).
        if (isOwner.Value)
        {
            var all = await db.Permissions
                .Where(p => p.Status == PermissionStatus.Active)
                .Select(p => p.Key)
                .ToListAsync(ct);
            return _cache = all.ToHashSet();
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

        return _cache = keys.ToHashSet();
    }

    public async Task<bool> HasAsync(string permissionKey, CancellationToken ct)
        => (await ForCurrentUserAsync(ct)).Contains(permissionKey);
}

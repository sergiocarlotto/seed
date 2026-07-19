using Microsoft.EntityFrameworkCore;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Reconcilia o catálogo do código (fonte de verdade) na tabela Permission.
// Idempotente: insere novas, atualiza metadados, reativa as que reaparecem e
// marca como Obsolete as que sumiram do código. Núcleo separado do hosted
// service para ser testável com catálogos arbitrários.
public static class PermissionCatalogReconciler
{
    public static async Task ReconcileAsync(
        SeedDbContext db,
        IReadOnlyList<PermissionDefinition> defs,
        CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, ct);
        var declared = defs.Select(d => d.Key).ToHashSet();

        foreach (var d in defs)
        {
            if (existing.TryGetValue(d.Key, out var p))
            {
                p.Module = d.Module;
                p.DisplayName = d.DisplayName;
                p.Description = d.Description;
                p.Status = PermissionStatus.Active; // reativa se estava Obsolete
            }
            else
            {
                db.Permissions.Add(new Permission
                {
                    Key = d.Key,
                    Module = d.Module,
                    DisplayName = d.DisplayName,
                    Description = d.Description,
                    Status = PermissionStatus.Active,
                });
            }
        }

        foreach (var (key, p) in existing)
            if (!declared.Contains(key))
                p.Status = PermissionStatus.Obsolete;

        await db.SaveChangesAsync(ct);
    }
}

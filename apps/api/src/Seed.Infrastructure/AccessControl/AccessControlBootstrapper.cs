using Microsoft.EntityFrameworkCore;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Garante, para cada organização, o perfil de sistema "Administrador" com todas
// as permissões ativas, e marca os usuários orgRole=Admin como owner, ligando-os
// ao perfil. Idempotente: roda todo boot sem duplicar. Deve rodar APÓS o
// reconciliador do catálogo (precisa das permissões já projetadas na tabela).
//
// Premissa: cobre apenas as organizações e usuários já existentes no momento do
// boot. Uma organização criada em runtime (ex.: futuro fluxo de onboarding) só
// ganha o perfil "Administrador" no próximo boot, a menos que esse fluxo chame
// AccessControlBootstrapper.SeedAsync explicitamente.
public static class AccessControlBootstrapper
{
    public const string AdminProfileName = "Administrador";

    public static async Task SeedAsync(SeedDbContext db, CancellationToken ct)
    {
        var activeKeys = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .Select(p => p.Key)
            .ToListAsync(ct);

        var orgIds = await db.Organizations.Select(o => o.Id).ToListAsync(ct);

        foreach (var orgId in orgIds)
        {
            // 1. Garante o perfil de sistema "Administrador" da organização.
            // Lookup por (OrganizationId, IsSystem, Name), alinhado ao índice único
            // (OrganizationId, Name) do Profile — evita ambiguidade se um dia
            // existir mais de um perfil is_system por organização.
            var adminProfile = await db.Profiles
                .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IsSystem && p.Name == AdminProfileName, ct);
            if (adminProfile is null)
            {
                var now = DateTime.UtcNow;
                adminProfile = new Profile
                {
                    OrganizationId = orgId,
                    Name = AdminProfileName,
                    Description = "Perfil de sistema com todas as permissões.",
                    IsSystem = true,
                    Status = ProfileStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Profiles.Add(adminProfile);
                // O Id já é gerado no construtor de Entity (client-generated); este
                // SaveChanges persiste o perfil antes de referenciá-lo nos vínculos
                // (ProfilePermission/UserProfile) abaixo, não para obter o Id.
                await db.SaveChangesAsync(ct);
            }

            // 2. Top-up: o "Administrador" concede todas as permissões ativas.
            var granted = await db.ProfilePermissions
                .Where(pp => pp.ProfileId == adminProfile.Id)
                .Select(pp => pp.PermissionKey)
                .ToListAsync(ct);
            foreach (var key in activeKeys.Except(granted))
                db.ProfilePermissions.Add(new ProfilePermission
                {
                    ProfileId = adminProfile.Id,
                    PermissionKey = key,
                });

            // 3. Admins da org viram owner e são ligados ao perfil "Administrador".
            var admins = await db.Users
                .Where(u => u.OrganizationId == orgId && u.OrgRole == OrganizationRole.Admin)
                .ToListAsync(ct);
            foreach (var user in admins)
            {
                if (!user.IsOwner) user.IsOwner = true;

                var linked = await db.UserProfiles
                    .AnyAsync(up => up.UserId == user.Id && up.ProfileId == adminProfile.Id, ct);
                if (!linked)
                    db.UserProfiles.Add(new UserProfile
                    {
                        UserId = user.Id,
                        ProfileId = adminProfile.Id,
                    });
            }

            await db.SaveChangesAsync(ct);
        }
    }
}

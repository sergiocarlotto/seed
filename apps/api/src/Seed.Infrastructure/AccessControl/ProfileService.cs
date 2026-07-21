using Microsoft.EntityFrameworkCore;
using Npgsql;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Application.Audit;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

public class ProfileService(
    SeedDbContext db, ICurrentUser currentUser, IClock clock, IAuditLog audit) : IProfileService
{
    private const string EntityType = "Profile";

    private async Task<Guid> OrgIdAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new ProfileForbiddenException("Não autenticado.");
        var orgId = await db.Users.Where(u => u.Id == userId)
            .Select(u => (Guid?)u.OrganizationId).FirstOrDefaultAsync(ct);
        return orgId ?? throw new ProfileForbiddenException("Usuário sem organização.");
    }

    public async Task<IReadOnlyList<ProfileSummaryDto>> ListAsync(CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var rows = await db.Profiles
            .Where(p => p.OrganizationId == orgId)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.IsSystem, p.Status,
                UserCount = db.UserProfiles.Count(up => up.ProfileId == p.Id),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ProfileSummaryDto(
                r.Id, r.Name, r.Description, r.IsSystem, r.Status.ToString(), r.UserCount))
            .ToList();
    }

    public async Task<ProfileDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (p is null) return null;
        var keys = await KeysOfAsync(id, ct);
        return Map(p, keys);
    }

    public async Task<ProfileDetailDto> CreateAsync(CreateProfileRequest req, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) throw new ProfileValidationException("Nome obrigatório.");
        if (await db.Profiles.AnyAsync(p => p.OrganizationId == orgId && p.Name == name, ct))
            throw new ProfileValidationException("Já existe um perfil com esse nome.");

        var keys = await ValidateKeysAsync(req.PermissionKeys, ct);
        var desc = req.Description?.Trim() ?? "";
        var now = clock.UtcNow;

        var profile = new Profile
        {
            OrganizationId = orgId, Name = name, Description = desc,
            IsSystem = false, Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        foreach (var k in keys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = k });

        audit.Record(orgId, "access_control.profile.created", EntityType, profile.Id.ToString(),
            new { name, description = desc });
        foreach (var k in keys)
            audit.Record(orgId, "access_control.profile.permission_granted", EntityType, profile.Id.ToString(),
                new { permission_key = k, profile_name = name, old = false, @new = true });

        await SaveTranslatingDuplicateNameAsync(ct);
        return Map(profile, keys);
    }

    public async Task<ProfileDetailDto?> UpdateAsync(Guid id, UpdateProfileRequest req, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (profile is null) return null;
        if (profile.IsSystem) throw new ProfileValidationException("Perfil de sistema não pode ser editado.");

        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) throw new ProfileValidationException("Nome obrigatório.");
        if (await db.Profiles.AnyAsync(p => p.OrganizationId == orgId && p.Name == name && p.Id != id, ct))
            throw new ProfileValidationException("Já existe um perfil com esse nome.");
        var desc = req.Description?.Trim() ?? "";
        var newKeys = await ValidateKeysAsync(req.PermissionKeys, ct);

        if (name != profile.Name)
            audit.Record(orgId, "access_control.profile.updated", EntityType, id.ToString(),
                new { field = "name", old = profile.Name, @new = name });
        if (desc != profile.Description)
            audit.Record(orgId, "access_control.profile.updated", EntityType, id.ToString(),
                new { field = "description", old = profile.Description, @new = desc });
        profile.Name = name;
        profile.Description = desc;
        profile.UpdatedAt = clock.UtcNow;

        var current = await KeysOfAsync(id, ct);
        foreach (var k in newKeys.Except(current))
        {
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = id, PermissionKey = k });
            audit.Record(orgId, "access_control.profile.permission_granted", EntityType, id.ToString(),
                new { permission_key = k, profile_name = name, old = false, @new = true });
        }
        foreach (var k in current.Except(newKeys))
        {
            var row = await db.ProfilePermissions.FirstAsync(pp => pp.ProfileId == id && pp.PermissionKey == k, ct);
            db.ProfilePermissions.Remove(row);
            audit.Record(orgId, "access_control.profile.permission_revoked", EntityType, id.ToString(),
                new { permission_key = k, profile_name = name, old = true, @new = false });
        }

        await SaveTranslatingDuplicateNameAsync(ct);
        return Map(profile, newKeys);
    }

    public async Task<bool> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (profile is null) return false;
        if (profile.IsSystem) throw new ProfileValidationException("Perfil de sistema não pode ser arquivado.");
        if (profile.Status == ProfileStatus.Archived) return true;

        var oldStatus = profile.Status.ToString();
        profile.Status = ProfileStatus.Archived;
        profile.UpdatedAt = clock.UtcNow;
        audit.Record(orgId, "access_control.profile.archived", EntityType, id.ToString(),
            new { field = "status", old = oldStatus, @new = ProfileStatus.Archived.ToString() });

        await db.SaveChangesAsync(ct);
        return true;
    }

    // O AnyAsync de nome único e o SaveChangesAsync não são atômicos: sob
    // concorrência, duas requisições podem passar a checagem e colidir no índice
    // único parcial (OrganizationId, Name) do Postgres. Traduz essa corrida
    // (23505) para a mesma ProfileValidationException do caminho não-concorrente
    // em vez de deixar vazar como 500.
    private async Task SaveTranslatingDuplicateNameAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ProfileValidationException("Já existe um perfil com esse nome.");
        }
    }

    private async Task<List<string>> KeysOfAsync(Guid profileId, CancellationToken ct) =>
        await db.ProfilePermissions.Where(pp => pp.ProfileId == profileId)
            .Select(pp => pp.PermissionKey).ToListAsync(ct);

    private async Task<List<string>> ValidateKeysAsync(IReadOnlyList<string>? keys, CancellationToken ct)
    {
        var distinct = (keys ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (distinct.Count == 0) return distinct;

        var valid = await db.Permissions
            .Where(p => distinct.Contains(p.Key) && p.Status == PermissionStatus.Active)
            .Select(p => p.Key).ToListAsync(ct);

        var invalid = distinct.Except(valid).ToList();
        if (invalid.Count > 0)
            throw new ProfileValidationException(
                $"Permissões inválidas ou obsoletas: {string.Join(", ", invalid)}");
        return valid;
    }

    private static ProfileDetailDto Map(Profile p, List<string> keys) =>
        new(p.Id, p.Name, p.Description, p.IsSystem, p.Status.ToString(), keys);
}

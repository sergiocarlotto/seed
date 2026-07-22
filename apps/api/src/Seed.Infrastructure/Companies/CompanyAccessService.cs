using Microsoft.EntityFrameworkCore;
using Npgsql;
using Seed.Application.Abstractions;
using Seed.Application.Audit;
using Seed.Application.Companies;
using Seed.Domain.Access;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Companies;

// Concessão de acesso a empresa. A autorização funcional (companies.grant_access)
// é feita no gate do controller; aqui mora a tenancy e o escopo concedível da
// ADR-0014, que é a trava contra autoconcessão a empresas alheias.
public class CompanyAccessService(
    SeedDbContext db, ICurrentUser currentUser, IClock clock, IAuditLog audit) : ICompanyAccessService
{
    private const string EntityType = "User";

    private async Task<(Guid OrgId, Guid CallerId, bool IsOwner)> CallerAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new CompanyAccessNotFoundException("Não autenticado.");
        var caller = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.OrganizationId, u.IsOwner })
            .FirstOrDefaultAsync(ct)
            ?? throw new CompanyAccessNotFoundException("Usuário sem organização.");
        return (caller.OrganizationId, userId, caller.IsOwner);
    }

    // Escopo concedível (ADR-0014): owner alcança toda a organização; os demais,
    // apenas as empresas do próprio acesso. O filtro global de Company já exclui
    // as soft-deleted, então elas ficam fora do escopo por construção. A org é
    // exigida nos DOIS lados do join: a linha de acesso pode, em tese, apontar
    // para empresa de outro tenant, e o escopo é a trava que não pode ceder.
    private async Task<HashSet<Guid>> GrantableScopeAsync(
        Guid orgId, Guid callerId, bool isOwner, CancellationToken ct)
    {
        if (isOwner)
            return (await db.Companies.Where(c => c.OrganizationId == orgId)
                .Select(c => c.Id).ToListAsync(ct)).ToHashSet();

        return (await (
            from a in db.UserCompanyAccesses
            join c in db.Companies on a.CompanyId equals c.Id
            where a.UserId == callerId && a.OrganizationId == orgId && c.OrganizationId == orgId
            select c.Id).ToListAsync(ct)).ToHashSet();
    }

    public async Task SetUserCompaniesAsync(Guid userId, SetUserCompaniesRequest req, CancellationToken ct)
    {
        var (orgId, callerId, isOwner) = await CallerAsync(ct);

        var targetExists = await db.Users.AnyAsync(u => u.Id == userId && u.OrganizationId == orgId, ct);
        if (!targetExists)
            throw new CompanyAccessNotFoundException("Usuário inexistente nesta organização.");

        var scope = await GrantableScopeAsync(orgId, callerId, isOwner, ct);
        var requested = (req.CompanyIds ?? []).Distinct().ToList();

        // Empresa fora do escopo é indistinguível de inexistente (ADR-0014).
        if (requested.Any(id => !scope.Contains(id)))
            throw new CompanyAccessNotFoundException("Empresa inexistente nesta organização.");

        var current = await db.UserCompanyAccesses
            .Where(a => a.UserId == userId && a.OrganizationId == orgId)
            .Select(a => a.CompanyId).ToListAsync(ct);

        // Só o que está no escopo entra no cálculo de remoção: o que o chamador
        // não enxerga é preservado, não removido por ausência no payload.
        var currentInScope = current.Where(scope.Contains).ToList();

        var toAdd = requested.Except(current).ToList();
        var toRemove = currentInScope.Except(requested).ToList();

        await ApplyAsync(orgId, userId, toAdd, toRemove, ct);
    }

    public async Task<IReadOnlyList<CompanyUserAccessDto>> ListCompanyUsersAsync(
        Guid companyId, CancellationToken ct)
    {
        var (orgId, callerId, isOwner) = await CallerAsync(ct);
        var scope = await GrantableScopeAsync(orgId, callerId, isOwner, ct);
        if (!scope.Contains(companyId))
            throw new CompanyAccessNotFoundException("Empresa inexistente nesta organização.");

        var granted = await db.UserCompanyAccesses
            .Where(a => a.CompanyId == companyId && a.OrganizationId == orgId)
            .Select(a => a.UserId).ToListAsync(ct);
        var grantedSet = granted.ToHashSet();

        var users = await db.Users.Where(u => u.OrganizationId == orgId)
            .OrderBy(u => u.FullName).ThenBy(u => u.Email)
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync(ct);

        return users
            .Select(u => new CompanyUserAccessDto(u.Id, u.FullName, u.Email ?? "", grantedSet.Contains(u.Id)))
            .ToList();
    }

    public async Task SetCompanyUsersAsync(Guid companyId, SetCompanyUsersRequest req, CancellationToken ct)
    {
        var (orgId, callerId, isOwner) = await CallerAsync(ct);
        var scope = await GrantableScopeAsync(orgId, callerId, isOwner, ct);
        if (!scope.Contains(companyId))
            throw new CompanyAccessNotFoundException("Empresa inexistente nesta organização.");

        var requested = (req.UserIds ?? []).Distinct().ToList();
        var validCount = requested.Count == 0 ? 0 : await db.Users
            .CountAsync(u => requested.Contains(u.Id) && u.OrganizationId == orgId, ct);
        if (validCount != requested.Count)
            throw new CompanyAccessNotFoundException("Usuário inexistente nesta organização.");

        var current = await db.UserCompanyAccesses
            .Where(a => a.CompanyId == companyId && a.OrganizationId == orgId)
            .Select(a => a.UserId).ToListAsync(ct);

        // A empresa está no escopo, então todos os usuários da organização são
        // alvo legítimo: aqui o conjunto é completo, sem preservação.
        foreach (var uid in requested.Except(current))
            await ApplyAsync(orgId, uid, [companyId], [], ct, save: false);
        foreach (var uid in current.Except(requested))
            await ApplyAsync(orgId, uid, [], [companyId], ct, save: false);

        await SaveAsync(ct);
    }

    // Aplica o delta de UM usuário. save: false acumula no change tracker para
    // que o chamador persista tudo numa única unidade de trabalho (ADR-0013).
    private async Task ApplyAsync(
        Guid orgId, Guid userId, List<Guid> toAdd, List<Guid> toRemove,
        CancellationToken ct, bool save = true)
    {
        var touched = toAdd.Concat(toRemove).Distinct().ToList();
        if (touched.Count == 0)
        {
            if (save) await SaveAsync(ct);
            return;
        }

        var names = await db.Companies.Where(c => touched.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var now = clock.UtcNow;

        foreach (var cid in toAdd)
        {
            db.UserCompanyAccesses.Add(new UserCompanyAccess
            {
                UserId = userId, CompanyId = cid, OrganizationId = orgId,
                CreatedAt = now, UpdatedAt = now,
            });
            audit.Record(orgId, "organizations.user.company_access_granted", EntityType,
                userId.ToString(),
                new
                {
                    company_id = cid, company_name = names.GetValueOrDefault(cid),
                    old = false, @new = true,
                });
        }

        foreach (var cid in toRemove)
        {
            var row = await db.UserCompanyAccesses.FirstOrDefaultAsync(
                a => a.UserId == userId && a.CompanyId == cid && a.OrganizationId == orgId, ct);
            if (row is null) continue; // já removida concorrentemente
            db.UserCompanyAccesses.Remove(row);
            audit.Record(orgId, "organizations.user.company_access_revoked", EntityType,
                userId.ToString(),
                new
                {
                    company_id = cid, company_name = names.GetValueOrDefault(cid),
                    old = true, @new = false,
                });
        }

        if (save) await SaveAsync(ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Corrida de concessão idêntica: o índice único (UserId, CompanyId)
            // barrou a segunda inserção.
            throw new CompanyAccessConflictException("Conflito ao atualizar os acessos. Tente novamente.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CompanyAccessConflictException("Conflito ao atualizar os acessos. Tente novamente.");
        }
    }
}

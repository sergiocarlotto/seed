using Microsoft.EntityFrameworkCore;
using Seed.Application.Companies;
using Seed.Domain.Access;
using Seed.Domain.Companies;

namespace Seed.Infrastructure.Persistence;

public class CompanyRepository(SeedDbContext db) : ICompanyRepository
{
    // Bypass de leitura do owner (ADR-0014, regra 3). O eixo de empresa passou a
    // ser revogável: se TODA concessão de uma empresa some, ela vira uma "empresa
    // órfã" — invisível na listagem, e sem o id nenhum endpoint de concessão pode
    // ser chamado. A ADR promete que o owner destrava esse caso, mas ele só tinha
    // escopo total de ESCRITA; sem um caminho de leitura o destravamento exigiria
    // o banco. Daí o ramo abaixo, restrito a quem é owner.
    //
    // O filtro por OrganizationId é OBRIGATÓRIO neste ramo. A consulta por
    // concessão é intra-organização por construção (a linha de UserCompanyAccess
    // nasce presa a uma org), então ela nunca precisou do filtro; o ramo do owner
    // varre db.Companies direto e, sem a org, devolveria as empresas de TODOS os
    // tenants. A organização vem do banco, do registro do próprio chamador, nunca
    // de parâmetro que o chamador controle.
    private async Task<(Guid OrganizationId, bool IsOwner)?> CallerAsync(
        Guid userId, CancellationToken ct)
    {
        var caller = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.OrganizationId, u.IsOwner })
            .FirstOrDefaultAsync(ct);
        return caller is null ? null : (caller.OrganizationId, caller.IsOwner);
    }

    public async Task<List<Company>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var caller = await CallerAsync(userId, ct);
        if (caller is null) return [];

        if (caller.Value.IsOwner)
            return await db.Companies
                .Where(c => c.OrganizationId == caller.Value.OrganizationId)
                .OrderBy(c => c.Name)
                .ToListAsync(ct);

        return await (from a in db.UserCompanyAccesses
                      join c in db.Companies on a.CompanyId equals c.Id
                      where a.UserId == userId
                      orderby c.Name
                      select c).ToListAsync(ct);
    }

    public async Task<Company?> GetForUserAsync(Guid companyId, Guid userId, CancellationToken ct)
    {
        var caller = await CallerAsync(userId, ct);
        if (caller is null) return null;

        // Mesma regra do List: id certo NÃO basta: a empresa precisa ser da
        // organização do chamador, senão o bypass viraria alcance cross-tenant.
        if (caller.Value.IsOwner)
            return await db.Companies.FirstOrDefaultAsync(
                c => c.Id == companyId && c.OrganizationId == caller.Value.OrganizationId, ct);

        return await (from c in db.Companies
                      where c.Id == companyId
                         && db.UserCompanyAccesses.Any(a => a.CompanyId == companyId && a.UserId == userId)
                      select c).FirstOrDefaultAsync(ct);
    }

    public async Task<UserContext?> GetUserContextAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        return u is null ? null : new UserContext(u.OrganizationId);
    }

    public async Task AddAsync(Company company, UserCompanyAccess access, CancellationToken ct)
    {
        await db.Companies.AddAsync(company, ct);
        await db.UserCompanyAccesses.AddAsync(access, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

using Seed.Application.Abstractions;
using Seed.Domain.Access;
using Seed.Domain.Companies;
using Seed.Domain.Organizations;

namespace Seed.Application.Companies;

public class ForbiddenException(string message) : Exception(message);

public interface ICompanyService
{
    Task<List<CompanyDto>> ListAsync(CancellationToken ct);
    Task<CompanyDto?> GetAsync(Guid id, CancellationToken ct);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest req, CancellationToken ct);
    Task<CompanyDto?> UpdateAsync(Guid id, UpdateCompanyRequest req, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

public class CompanyService(
    ICompanyRepository repo,
    ICurrentUser currentUser,
    IClock clock) : ICompanyService
{
    private Guid UserId => currentUser.UserId ?? throw new ForbiddenException("Não autenticado.");

    public async Task<List<CompanyDto>> ListAsync(CancellationToken ct) =>
        (await repo.ListForUserAsync(UserId, ct)).Select(Map).ToList();

    public async Task<CompanyDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await repo.GetForUserAsync(id, UserId, ct);
        return c is null ? null : Map(c);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest req, CancellationToken ct)
    {
        // Ainda precisamos da organização do usuário para criar a empresa sob o
        // tenant correto. A autorização (companies.manage) já foi feita no gate.
        var ctx = await repo.GetUserContextAsync(UserId, ct)
            ?? throw new ForbiddenException("Usuário sem organização.");

        var now = clock.UtcNow;
        var company = new Company { OrganizationId = ctx.OrganizationId, Name = req.Name.Trim(), CreatedAt = now, UpdatedAt = now };
        var access = new UserCompanyAccess { UserId = UserId, CompanyId = company.Id, OrganizationId = ctx.OrganizationId, CreatedAt = now, UpdatedAt = now };
        await repo.AddAsync(company, access, ct);
        await repo.SaveChangesAsync(ct);
        return Map(company);
    }

    public async Task<CompanyDto?> UpdateAsync(Guid id, UpdateCompanyRequest req, CancellationToken ct)
    {
        // Autorização funcional no gate (companies.manage); aqui só o eixo de
        // empresa: sem acesso à empresa alvo → null → 404 (não vaza existência).
        var c = await repo.GetForUserAsync(id, UserId, ct);
        if (c is null) return null;
        c.Name = req.Name.Trim();
        c.UpdatedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var c = await repo.GetForUserAsync(id, UserId, ct);
        if (c is null) return false;
        c.DeletedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return true;
    }

    private static CompanyDto Map(Company c) =>
        new(c.Id, c.Name, c.Status.ToString(), c.CreatedAt, c.UpdatedAt);
}

using Seed.Domain.Access;
using Seed.Domain.Companies;

namespace Seed.Application.Companies;

public interface ICompanyRepository
{
    Task<List<Company>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<Company?> GetForUserAsync(Guid companyId, Guid userId, CancellationToken ct);
    Task<UserContext?> GetUserContextAsync(Guid userId, CancellationToken ct);
    Task AddAsync(Company company, UserCompanyAccess access, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

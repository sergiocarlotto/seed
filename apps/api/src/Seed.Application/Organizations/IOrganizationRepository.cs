using Seed.Domain.Organizations;

namespace Seed.Application.Organizations;

public interface IOrganizationRepository
{
    Task<List<(Organization Org, OrganizationRole Role)>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<Organization?> GetByIdForUserAsync(Guid orgId, Guid userId, CancellationToken ct);
    Task<OrganizationRole?> GetRoleAsync(Guid orgId, Guid userId, CancellationToken ct);
    Task AddAsync(Organization org, OrganizationMembership ownerMembership, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

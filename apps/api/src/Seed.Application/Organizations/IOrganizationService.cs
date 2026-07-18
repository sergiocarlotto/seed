namespace Seed.Application.Organizations;

public interface IOrganizationService
{
    Task<List<OrganizationDto>> ListAsync(CancellationToken ct);
    Task<OrganizationDto?> GetAsync(Guid id, CancellationToken ct);
    Task<OrganizationDto> CreateAsync(CreateOrganizationRequest req, CancellationToken ct);
    Task<OrganizationDto> CreateAsync(Guid ownerUserId, CreateOrganizationRequest req, CancellationToken ct);
    Task<OrganizationDto?> UpdateAsync(Guid id, UpdateOrganizationRequest req, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

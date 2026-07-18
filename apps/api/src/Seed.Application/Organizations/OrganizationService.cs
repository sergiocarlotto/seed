using Seed.Application.Abstractions;
using Seed.Domain.Organizations;
using Seed.Domain.Memberships;

namespace Seed.Application.Organizations;

public class ForbiddenException(string message) : Exception(message);

public class OrganizationService(
    IOrganizationRepository repo,
    ICurrentUser currentUser,
    IClock clock) : IOrganizationService
{
    private Guid UserId => currentUser.UserId ?? throw new ForbiddenException("Não autenticado.");

    public async Task<List<OrganizationDto>> ListAsync(CancellationToken ct)
    {
        var items = await repo.ListForUserAsync(UserId, ct);
        return items.Select(i => Map(i.Org, i.Role)).ToList();
    }

    public async Task<OrganizationDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var role = await repo.GetRoleAsync(id, UserId, ct);
        if (role is null) return null;
        var org = await repo.GetByIdForUserAsync(id, UserId, ct);
        return org is null ? null : Map(org, role.Value);
    }

    public Task<OrganizationDto> CreateAsync(CreateOrganizationRequest req, CancellationToken ct) =>
        CreateAsync(UserId, req, ct);

    public async Task<OrganizationDto> CreateAsync(Guid ownerUserId, CreateOrganizationRequest req, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var org = new Organization { Name = req.Name.Trim(), CreatedAt = now, UpdatedAt = now };
        var membership = new OrganizationMembership
        {
            OrganizationId = org.Id, UserId = ownerUserId,
            Role = OrganizationRole.Owner, Status = MembershipStatus.Active,
            CreatedAt = now, UpdatedAt = now
        };
        await repo.AddAsync(org, membership, ct);
        await repo.SaveChangesAsync(ct);
        return Map(org, OrganizationRole.Owner);
    }

    public async Task<OrganizationDto?> UpdateAsync(Guid id, UpdateOrganizationRequest req, CancellationToken ct)
    {
        var role = await repo.GetRoleAsync(id, UserId, ct);
        if (role is null) return null;
        if (role is not (OrganizationRole.Owner or OrganizationRole.Admin))
            throw new ForbiddenException("Sem permissão para editar.");
        var org = await repo.GetByIdForUserAsync(id, UserId, ct);
        if (org is null) return null;
        org.Name = req.Name.Trim();
        org.UpdatedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return Map(org, role.Value);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await repo.GetRoleAsync(id, UserId, ct);
        if (role is null) return false;
        if (role is not OrganizationRole.Owner)
            throw new ForbiddenException("Apenas o owner pode excluir.");
        var org = await repo.GetByIdForUserAsync(id, UserId, ct);
        if (org is null) return false;
        org.DeletedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return true;
    }

    private static OrganizationDto Map(Organization o, OrganizationRole role) =>
        new(o.Id, o.Name, o.Status.ToString(), role.ToString(), o.CreatedAt, o.UpdatedAt);
}

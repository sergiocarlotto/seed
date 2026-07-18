namespace Seed.Application.Organizations;

public record CreateOrganizationRequest(string Name);
public record UpdateOrganizationRequest(string Name);
public record OrganizationDto(Guid Id, string Name, string Status, string Role, DateTime CreatedAt, DateTime UpdatedAt);
public record MembershipDto(Guid OrganizationId, string OrganizationName, string Role);

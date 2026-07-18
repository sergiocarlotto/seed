using Seed.Domain.Organizations;

namespace Seed.Application.Companies;

public record UserContext(Guid OrganizationId, OrganizationRole OrgRole);

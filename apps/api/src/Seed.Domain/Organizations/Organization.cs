using Seed.Domain.Common;

namespace Seed.Domain.Organizations;

public class Organization : Entity
{
    public string Name { get; set; } = string.Empty;
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();
}

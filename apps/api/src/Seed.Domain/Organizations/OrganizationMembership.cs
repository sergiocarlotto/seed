using Seed.Domain.Common;
using Seed.Domain.Memberships;

namespace Seed.Domain.Organizations;

public class OrganizationMembership : Entity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid UserId { get; set; }
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
}

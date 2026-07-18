using Seed.Domain.Common;

namespace Seed.Domain.Access;

public class UserCompanyAccess : Entity
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid OrganizationId { get; set; }
}

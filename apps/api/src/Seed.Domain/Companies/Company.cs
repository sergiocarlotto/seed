using Seed.Domain.Common;

namespace Seed.Domain.Companies;

public class Company : Entity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CompanyStatus Status { get; set; } = CompanyStatus.Active;
}

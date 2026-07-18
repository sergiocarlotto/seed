using Seed.Domain.Common;
using Seed.Domain.Companies;

namespace Seed.Domain.Organizations;

public class Organization : Entity
{
    public string Name { get; set; } = string.Empty;
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public ICollection<Company> Companies { get; set; } = new List<Company>();
}

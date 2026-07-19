using Microsoft.AspNetCore.Identity;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public OrganizationRole OrgRole { get; set; } = OrganizationRole.Member;

    // Dono da organização. Gerido fora da aplicação (banco/superadmin externo);
    // nunca setado via API. Tem bypass funcional; é somente-leitura na gestão.
    public bool IsOwner { get; set; }
}

using Microsoft.AspNetCore.Identity;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public OrganizationRole OrgRole { get; set; } = OrganizationRole.Member;

    // Dono da organização. Semeado no boot para usuários orgRole=Admin pelo
    // AccessControlBootstrapper (Seed.Infrastructure.AccessControl); nenhum
    // endpoint de API o altera. Tem bypass funcional total.
    public bool IsOwner { get; set; }

    // Situação do usuário. Inactive é setado via PATCH /users/{id}/status
    // (users.manage). Refletido na resolução da permissão efetiva
    // (EffectivePermissionsService) e no login → bloqueio imediato.
    public UserStatus Status { get; set; } = UserStatus.Active;
}

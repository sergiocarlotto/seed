using Microsoft.AspNetCore.Identity;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }

    // Dono da organização. Semeado pelo DataSeeder/banco (gerido fora da app);
    // nenhum endpoint de API o altera. Tem bypass funcional total. O
    // AccessControlBootstrapper liga os owners ao perfil "Administrador" no boot.
    public bool IsOwner { get; set; }

    // Situação do usuário. Inactive é setado via PATCH /users/{id}/status
    // (users.manage). Refletido na resolução da permissão efetiva e no login.
    public UserStatus Status { get; set; } = UserStatus.Active;
}

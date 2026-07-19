namespace Seed.Domain.AccessControl;

// M:N ApplicationUser <-> Profile. Permissão efetiva = união dos perfis ativos.
public class UserProfile
{
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
}

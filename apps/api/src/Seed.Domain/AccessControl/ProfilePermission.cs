namespace Seed.Domain.AccessControl;

// M:N Profile <-> Permission. A FK em PermissionKey (configurada no DbContext)
// é a trava contra conceder permissão inexistente.
public class ProfilePermission
{
    public Guid ProfileId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
}

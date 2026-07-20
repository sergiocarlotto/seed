namespace Seed.Domain.Organizations;

// Situação do usuário na organização. Inactive = desativado: acesso bloqueado
// imediatamente (permissão efetiva vazia) e login recusado. Armazenado como int
// (default 0 = Active), consistente com ProfileStatus/PermissionStatus.
public enum UserStatus
{
    Active = 0,
    Inactive = 1,
}

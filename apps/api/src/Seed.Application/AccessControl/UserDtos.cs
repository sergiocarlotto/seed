namespace Seed.Application.AccessControl;

// Referências enxutas para os chips da tela de usuários (perfis atribuídos e
// empresas acessíveis). Só id + nome — a gestão de cada um vive no seu módulo.
public record UserProfileRefDto(Guid Id, string Name);
public record UserCompanyRefDto(Guid Id, string Name);

// Item da listagem e detalhe do usuário (mesma forma). IsOwner marca o dono da
// organização (somente-leitura na app). Companies são apenas exibidas.
public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Status,
    bool IsOwner,
    IReadOnlyList<UserProfileRefDto> Profiles,
    IReadOnlyList<UserCompanyRefDto> Companies);

// Requests (allow-list — nada de IsOwner/Status/OrganizationId/IsSystem).
public record UpdateUserStatusRequest(bool Active);
public record AssignProfilesRequest(IReadOnlyList<Guid>? ProfileIds);

// Criação de usuário. Allow-list estrita: OrganizationId, IsOwner, Status e
// EmailConfirmed NÃO existem aqui — são fixados pelo servidor, então não há o
// que ignorar. Senha definida pelo administrador (sem convite por e-mail).
public record CreateUserRequest(string FullName, string Email, string Password);

namespace Seed.Application.AccessControl;

// Violação de regra de negócio na gestão de usuários (ex.: tentar gerir o owner
// pela aplicação). O controller mapeia para 400.
public class UserValidationException(string message) : Exception(message);

// Refusa por autorização insuficiente sem ser falta de permissão de rota:
// não-owner tentando mexer no perfil is_system (postura B). → 403.
public class UserForbiddenException(string message) : Exception(message);

// Recurso referenciado (usuário ou profile_id) fora da org do chamador. → 404,
// sem vazar existência (ADR-0010).
public class UserNotFoundException(string message) : Exception(message);

// Conflito de concorrência ao aplicar a mutação (ex.: duas atribuições
// simultâneas colidindo na PK composta ou removendo a mesma linha). → 409.
public class UserConflictException(string message) : Exception(message);

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);
    Task<UserDto?> GetAsync(Guid id, CancellationToken ct);
    // Ativa/desativa (soft). Recusa o owner. Retorna null se o usuário não é da org.
    Task<UserDto?> SetStatusAsync(Guid id, UpdateUserStatusRequest req, CancellationToken ct);
    // Define o CONJUNTO de perfis do usuário. Retorna null se o usuário não é da org.
    Task<UserDto?> SetProfilesAsync(Guid id, AssignProfilesRequest req, CancellationToken ct);
}

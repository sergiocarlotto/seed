namespace Seed.Application.AccessControl;

// Violação de regra de negócio do CRUD de perfis (nome duplicado, permissão
// inválida, perfil de sistema imutável). O controller mapeia para 400.
public class ProfileValidationException(string message) : Exception(message);

// Ausência de contexto do usuário (não autenticado / sem organização). → 403.
public class ProfileForbiddenException(string message) : Exception(message);

public interface IProfileService
{
    Task<IReadOnlyList<ProfileSummaryDto>> ListAsync(CancellationToken ct);
    Task<ProfileDetailDto?> GetAsync(Guid id, CancellationToken ct);
    Task<ProfileDetailDto> CreateAsync(CreateProfileRequest req, CancellationToken ct);
    Task<ProfileDetailDto?> UpdateAsync(Guid id, UpdateProfileRequest req, CancellationToken ct);
    Task<bool> ArchiveAsync(Guid id, CancellationToken ct);
}

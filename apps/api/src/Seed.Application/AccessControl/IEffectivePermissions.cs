namespace Seed.Application.AccessControl;

// Resolve o conjunto de permissões efetivas do usuário atual: união das
// permissões ativas dos perfis ativos vinculados, com bypass total para o owner.
// Recalculado por request (sem cache entre requests → revogação imediata).
public interface IEffectivePermissions
{
    Task<IReadOnlySet<string>> ForCurrentUserAsync(CancellationToken ct);
    Task<bool> HasAsync(string permissionKey, CancellationToken ct);
}

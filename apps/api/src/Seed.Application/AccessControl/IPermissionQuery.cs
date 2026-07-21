namespace Seed.Application.AccessControl;

// Lê o catálogo de permissões ATIVAS (a projeção reconciliada), agrupado por
// módulo, para alimentar a tela de edição de perfil.
public interface IPermissionQuery
{
    Task<IReadOnlyList<PermissionGroupDto>> ListActiveGroupedAsync(CancellationToken ct);
}

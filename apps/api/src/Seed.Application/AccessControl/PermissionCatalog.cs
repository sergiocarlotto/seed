namespace Seed.Application.AccessControl;

// Junta as declarações de todos os módulos. Ao adicionar um módulo novo com
// permissões, concatene suas Definitions aqui (ex.: CompaniesPermissions no
// Plano 2).
public class PermissionCatalog : IPermissionCatalog
{
    public IReadOnlyList<PermissionDefinition> All { get; } =
        [.. AccessControlPermissions.Definitions];
}

namespace Seed.Application.AccessControl;

// Agrega as permissões declaradas por todos os módulos. Fonte de verdade do
// catálogo; a tabela Permission é apenas a projeção reconciliada no boot.
public interface IPermissionCatalog
{
    IReadOnlyList<PermissionDefinition> All { get; }
}

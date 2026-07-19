namespace Seed.Domain.AccessControl;

// Projeção do catálogo de permissões declarado no código (reconciliada no boot).
// Chave estável como PK; global à instância (sem organization_id).
public class Permission
{
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PermissionStatus Status { get; set; } = PermissionStatus.Active;
}

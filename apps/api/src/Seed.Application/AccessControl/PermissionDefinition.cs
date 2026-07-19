namespace Seed.Application.AccessControl;

// Declaração de uma permissão no catálogo do código (fonte de verdade).
public record PermissionDefinition(string Key, string Module, string DisplayName, string Description);

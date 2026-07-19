namespace Seed.Application.AccessControl;

public record PermissionItemDto(string Key, string DisplayName, string Description);
public record PermissionGroupDto(string Module, IReadOnlyList<PermissionItemDto> Permissions);

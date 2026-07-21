namespace Seed.Application.AccessControl;

// Requests: só Name/Description/PermissionKeys (allow-list — IsSystem/Status/
// OrganizationId nunca vêm do cliente).
public record CreateProfileRequest(string Name, string? Description, IReadOnlyList<string>? PermissionKeys);
public record UpdateProfileRequest(string Name, string? Description, IReadOnlyList<string>? PermissionKeys);

public record ProfileSummaryDto(Guid Id, string Name, string Description, bool IsSystem, string Status, int UserCount);
public record ProfileDetailDto(Guid Id, string Name, string Description, bool IsSystem, string Status, IReadOnlyList<string> PermissionKeys);

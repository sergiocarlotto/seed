using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

public class PermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}

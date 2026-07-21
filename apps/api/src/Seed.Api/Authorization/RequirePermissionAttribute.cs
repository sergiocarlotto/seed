using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

// [RequirePermission("profiles.manage")] — exige a permissão funcional no
// backend. O enforcement de empresa (UserCompanyAccess) é aplicado à parte.
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";
    public RequirePermissionAttribute(string permissionKey) => Policy = PolicyPrefix + permissionKey;
}

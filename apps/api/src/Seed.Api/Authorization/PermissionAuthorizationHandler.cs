using Microsoft.AspNetCore.Authorization;
using Seed.Application.AccessControl;

namespace Seed.Api.Authorization;

public class PermissionAuthorizationHandler(IEffectivePermissions permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await permissions.HasAsync(requirement.PermissionKey, CancellationToken.None))
            context.Succeed(requirement);
    }
}

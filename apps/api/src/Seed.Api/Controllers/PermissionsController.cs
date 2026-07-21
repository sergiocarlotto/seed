using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.AccessControl;

namespace Seed.Api.Controllers;

[ApiController]
[Route("permissions")]
public class PermissionsController(IPermissionQuery query) : ControllerBase
{
    // Requer profiles.manage: só quem monta perfis precisa ver o catálogo.
    [HttpGet]
    [RequirePermission(AccessControlPermissions.ProfilesManage)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await query.ListActiveGroupedAsync(ct));
}

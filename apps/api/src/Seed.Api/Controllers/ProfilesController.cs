using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.AccessControl;

namespace Seed.Api.Controllers;

[ApiController]
[Route("profiles")]
[RequirePermission(AccessControlPermissions.ProfilesManage)]
public class ProfilesController(IProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var p = await service.GetAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProfileRequest req, CancellationToken ct)
    {
        try { var p = await service.CreateAsync(req, ct); return CreatedAtAction(nameof(Get), new { id = p.Id }, p); }
        catch (ProfileValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ProfileForbiddenException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProfileRequest req, CancellationToken ct)
    {
        try { var p = await service.UpdateAsync(id, req, ct); return p is null ? NotFound() : Ok(p); }
        catch (ProfileValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ProfileForbiddenException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        try { var ok = await service.ArchiveAsync(id, ct); return ok ? NoContent() : NotFound(); }
        catch (ProfileValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ProfileForbiddenException) { return Forbid(); }
    }
}

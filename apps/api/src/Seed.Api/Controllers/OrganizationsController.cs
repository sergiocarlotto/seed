using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Organizations;

namespace Seed.Api.Controllers;

[ApiController]
[Authorize]
[Route("organizations")]
public class OrganizationsController(IOrganizationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var org = await service.GetAsync(id, ct);
        return org is null ? NotFound() : Ok(org);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Nome obrigatório." });
        var org = await service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = org.Id }, org);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOrganizationRequest req, CancellationToken ct)
    {
        try
        {
            var org = await service.UpdateAsync(id, req, ct);
            return org is null ? NotFound() : Ok(org);
        }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await service.DeleteAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (ForbiddenException) { return Forbid(); }
    }
}

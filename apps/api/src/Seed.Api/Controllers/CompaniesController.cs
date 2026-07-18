using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Companies;

namespace Seed.Api.Controllers;

[ApiController]
[Authorize]
[Route("companies")]
public class CompaniesController(ICompanyService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await service.GetAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCompanyRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Nome obrigatório." });
        try { var c = await service.CreateAsync(req, ct); return CreatedAtAction(nameof(Get), new { id = c.Id }, c); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCompanyRequest req, CancellationToken ct)
    {
        try { var c = await service.UpdateAsync(id, req, ct); return c is null ? NotFound() : Ok(c); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { var ok = await service.DeleteAsync(id, ct); return ok ? NoContent() : NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }
}

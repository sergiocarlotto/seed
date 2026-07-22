using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Companies;

namespace Seed.Api.Controllers;

// Gate por permissão (substitui o antigo orgRole=Admin): companies.access para
// ver/listar; companies.manage para criar/editar/excluir. O eixo de empresa
// (UserCompanyAccess) continua aplicado no serviço — recurso fora do acesso → 404.
[ApiController]
[Route("companies")]
public class CompaniesController(
    ICompanyService service, ICompanyAccessService companyAccess) : ControllerBase
{
    [HttpGet]
    [RequirePermission(CompaniesPermissions.Access)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(CompaniesPermissions.Access)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await service.GetAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    [RequirePermission(CompaniesPermissions.Manage)]
    public async Task<IActionResult> Create(CreateCompanyRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Nome obrigatório." });
        try { var c = await service.CreateAsync(req, ct); return CreatedAtAction(nameof(Get), new { id = c.Id }, c); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CompaniesPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, UpdateCompanyRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Nome obrigatório." });
        var c = await service.UpdateAsync(id, req, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(CompaniesPermissions.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await service.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }

    // Quem tem acesso a esta empresa. Gate companies.grant_access (não
    // companies.manage): conceder acesso a dados e manter cadastro são poderes
    // de natureza diferente. Devolve o mínimo para escolher — id, nome e e-mail.
    [HttpGet("{id:guid}/users")]
    [RequirePermission(CompaniesPermissions.GrantAccess)]
    public async Task<IActionResult> ListUsers(Guid id, CancellationToken ct)
    {
        try { return Ok(await companyAccess.ListCompanyUsersAsync(id, ct)); }
        catch (CompanyAccessNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/users")]
    [RequirePermission(CompaniesPermissions.GrantAccess)]
    public async Task<IActionResult> SetUsers(Guid id, SetCompanyUsersRequest req, CancellationToken ct)
    {
        try
        {
            await companyAccess.SetCompanyUsersAsync(id, req, ct);
            return NoContent();
        }
        catch (CompanyAccessValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (CompanyAccessNotFoundException) { return NotFound(); }
        catch (CompanyAccessConflictException ex) { return Conflict(new { error = ex.Message }); }
    }
}

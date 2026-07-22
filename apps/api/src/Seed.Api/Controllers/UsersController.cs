using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.AccessControl;
using Seed.Application.Companies;

namespace Seed.Api.Controllers;

// Gestão de usuários. Dois gates distintos por método: users.manage para
// listar/ver/ativar-desativar; profiles.assign para atribuir perfis. Por isso o
// [RequirePermission] fica no método, não na classe.
[ApiController]
[Route("users")]
public class UsersController(IUserService service, ICompanyAccessService companyAccess) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var u = await service.GetAsync(id, ct);
        return u is null ? NotFound() : Ok(u);
    }

    [HttpPost]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct)
    {
        try
        {
            var u = await service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(Get), new { id = u.Id }, u);
        }
        catch (UserValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UserForbiddenException) { return Forbid(); }
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> SetStatus(Guid id, UpdateUserStatusRequest req, CancellationToken ct)
    {
        try
        {
            var u = await service.SetStatusAsync(id, req, ct);
            return u is null ? NotFound() : Ok(u);
        }
        catch (UserValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UserForbiddenException) { return Forbid(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/profiles")]
    [RequirePermission(AccessControlPermissions.ProfilesAssign)]
    public async Task<IActionResult> SetProfiles(Guid id, AssignProfilesRequest req, CancellationToken ct)
    {
        try
        {
            var u = await service.SetProfilesAsync(id, req, ct);
            return u is null ? NotFound() : Ok(u);
        }
        catch (UserValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UserForbiddenException) { return Forbid(); }
        catch (UserNotFoundException) { return NotFound(); }
        catch (UserConflictException ex) { return Conflict(new { error = ex.Message }); }
    }

    // Eixo de empresa: gate próprio (companies.grant_access), distinto de
    // users.manage. O conjunto vale DENTRO do escopo concedível (ADR-0014).
    // Responde 204: devolver o UserDto entregaria os chips de perfil do usuário
    // a quem só tem companies.grant_access, o mesmo cadastro que a tela espelho
    // (GET /companies/{id}/users) nega. O frontend não lê o corpo.
    [HttpPut("{id:guid}/companies")]
    [RequirePermission(CompaniesPermissions.GrantAccess)]
    public async Task<IActionResult> SetCompanies(Guid id, SetUserCompaniesRequest req, CancellationToken ct)
    {
        try
        {
            await companyAccess.SetUserCompaniesAsync(id, req, ct);
            return NoContent();
        }
        catch (CompanyAccessNotFoundException) { return NotFound(); }
        catch (CompanyAccessConflictException ex) { return Conflict(new { error = ex.Message }); }
    }
}

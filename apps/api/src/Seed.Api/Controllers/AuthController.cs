using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Application.Companies;
using Seed.Infrastructure.Identity;

namespace Seed.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ICompanyService companyService,
    ICurrentUser currentUser,
    IEffectivePermissions effective) : ControllerBase
{
    public record LoginRequest(string Email, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var result = await signInManager.PasswordSignInAsync(req.Email, req.Password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded) return Unauthorized();
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null || user.Status == Seed.Domain.Organizations.UserStatus.Inactive)
        {
            // Desfaz o cookie recém-emitido e responde como credencial inválida
            // (não revela que a conta existe mas está desativada).
            await signInManager.SignOutAsync();
            return Unauthorized();
        }
        return Ok(new { user = new { user!.Id, user.Email, user.FullName } });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(currentUser.UserId!.ToString()!);
        var companies = await companyService.ListAsync(ct);
        var permissions = await effective.ForCurrentUserAsync(ct);
        return Ok(new
        {
            user = new { user!.Id, user.Email, user.FullName },
            organizationId = user.OrganizationId,
            orgRole = user.OrgRole.ToString(),
            isOwner = user.IsOwner,
            permissions,
            companies
        });
    }
}

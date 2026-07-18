using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Abstractions;
using Seed.Application.Companies;
using Seed.Infrastructure.Identity;

namespace Seed.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ICompanyService companyService,
    ICurrentUser currentUser) : ControllerBase
{
    public record LoginRequest(string Email, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var result = await signInManager.PasswordSignInAsync(req.Email, req.Password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded) return Unauthorized();
        var user = await userManager.FindByEmailAsync(req.Email);
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
        return Ok(new
        {
            user = new { user!.Id, user.Email, user.FullName },
            organizationId = user.OrganizationId,
            orgRole = user.OrgRole.ToString(),
            companies
        });
    }
}

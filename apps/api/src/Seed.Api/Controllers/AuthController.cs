using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Abstractions;
using Seed.Application.Organizations;
using Seed.Infrastructure.Identity;

namespace Seed.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOrganizationService organizations,
    ICurrentUser currentUser) : ControllerBase
{
    public record RegisterRequest(string OrganizationName, string FullName, string Email, string Password);
    public record LoginRequest(string Email, string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, FullName = req.FullName };
        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await signInManager.SignInAsync(user, isPersistent: true);
        // SignInAsync grava o cookie na resposta, mas não popula HttpContext.User
        // neste request; por isso passamos o user.Id explicitamente ao serviço.
        var org = await organizations.CreateAsync(user.Id, new CreateOrganizationRequest(req.OrganizationName), ct);
        return Ok(new { user = new { user.Id, user.Email, user.FullName }, organization = org });
    }

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
        var orgs = await organizations.ListAsync(ct);
        var memberships = orgs.Select(o => new MembershipDto(o.Id, o.Name, o.Role));
        return Ok(new { user = new { user!.Id, user.Email, user.FullName }, memberships });
    }
}

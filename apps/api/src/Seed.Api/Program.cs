using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Seed.Api;
using Seed.Application;
using Seed.Application.Abstractions;
using Seed.Infrastructure;
using Seed.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
    // API: responder com status em vez de redirecionar para página de login.
    o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider,
    Seed.Api.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    Seed.Api.Authorization.PermissionAuthorizationHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<SeedDbContext>().Database.Migrate();
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

// TLS is terminated by the reverse proxy (Caddy) per ADR-0007, so the API
// listens over plain HTTP inside the container. No HTTPS redirection here.

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

// Exposed so integration tests can bootstrap the API via WebApplicationFactory.
public partial class Program { }

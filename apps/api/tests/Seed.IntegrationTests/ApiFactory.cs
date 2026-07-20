using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Access;
using Seed.Domain.Companies;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Identity;
using Seed.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Seed.IntegrationTests;

// Sobe um Postgres real via Testcontainers e injeta a connection string na API.
// Roda em Development para que as migrations sejam aplicadas no startup e o
// DataSeeder crie a organização "Demo", o admin e a "Empresa Demo" (Program.cs).
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@demo.local";
    public const string AdminPassword = "Admin123!";
    public const string DemoCompanyName = "Empresa Demo";
    public const string DemoOrgName = "Demo";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // aplica migrations + seed no startup

        // No modelo de hosting mínimo, Program.cs lê a connection string de forma
        // eager em builder.Configuration, então ConfigureAppConfiguration não a
        // sobrescreve a tempo. Substituímos o registro do DbContext diretamente
        // pela connection string do container (o Postgres real do Testcontainers).
        builder.ConfigureTestServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<SeedDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddDbContext<SeedDbContext>(o => o.UseNpgsql(_db.GetConnectionString()));
        });
    }

    // Cria um HttpClient (mantém cookies por padrão) já autenticado via /auth/login.
    public async Task<HttpClient> CreateLoggedInClientAsync(string email, string password)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() =>
        CreateLoggedInClientAsync(AdminEmail, AdminPassword);

    // Id da organização "Demo" semeada no startup.
    public async Task<Guid> GetDemoOrganizationIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var org = await db.Organizations.FirstAsync(o => o.Name == DemoOrgName);
        return org.Id;
    }

    // Cria um usuário (com senha) numa organização existente. isOwner=false por
    // padrão (usuário comum, sem perfil e sem bypass). Não concede acesso a
    // nenhuma empresa.
    public async Task CreateUserAsync(string email, string password, Guid organizationId, bool isOwner = false)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = email,
            OrganizationId = organizationId,
            IsOwner = isOwner,
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Falha ao criar usuário {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    // Dados de uma segunda organização (tenant) criada para testes cross-tenant.
    public record SecondTenant(
        Guid OrganizationId,
        Guid CompanyId,
        string CompanyName,
        string UserEmail,
        string UserPassword);

    // Cria uma SEGUNDA organização + usuário admin + empresa + acesso explícito.
    public async Task<SecondTenant> CreateSecondTenantAsync(
        string orgName = "Outra Org",
        string companyName = "Empresa Outra",
        string userEmail = "outra@outra.local",
        string userPassword = "Outra123!")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var now = DateTime.UtcNow;

        var org = new Organization { Name = orgName, CreatedAt = now, UpdatedAt = now };
        db.Organizations.Add(org);

        var company = new Company { OrganizationId = org.Id, Name = companyName, CreatedAt = now, UpdatedAt = now };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = userEmail,
            Email = userEmail,
            EmailConfirmed = true,
            FullName = userEmail,
            OrganizationId = org.Id,
            IsOwner = true, // owner da nova org (gerido fora da app; bootstrap só roda no boot)
        };
        var result = await users.CreateAsync(user, userPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Falha ao criar usuário {userEmail}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        db.UserCompanyAccesses.Add(new UserCompanyAccess
        {
            UserId = user.Id,
            CompanyId = company.Id,
            OrganizationId = org.Id,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        return new SecondTenant(org.Id, company.Id, companyName, userEmail, userPassword);
    }

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync() => await _db.DisposeAsync();
}

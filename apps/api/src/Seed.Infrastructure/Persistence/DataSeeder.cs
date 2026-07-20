using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.Access;
using Seed.Domain.Companies;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Identity;

namespace Seed.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<SeedDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        if (await db.Organizations.AnyAsync()) return; // idempotente

        var now = DateTime.UtcNow;
        var org = new Organization { Name = "Demo", CreatedAt = now, UpdatedAt = now };
        db.Organizations.Add(org);

        var company = new Company { OrganizationId = org.Id, Name = "Empresa Demo", CreatedAt = now, UpdatedAt = now };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var admin = new ApplicationUser
        {
            UserName = "admin@demo.local", Email = "admin@demo.local",
            EmailConfirmed = true, FullName = "Admin Demo",
            OrganizationId = org.Id, IsOwner = true
        };
        await users.CreateAsync(admin, "Admin123!");

        db.UserCompanyAccesses.Add(new UserCompanyAccess
        {
            UserId = admin.Id, CompanyId = company.Id, OrganizationId = org.Id,
            CreatedAt = now, UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }
}

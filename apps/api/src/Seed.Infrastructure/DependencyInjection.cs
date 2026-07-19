using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Application.Companies;
using Seed.Infrastructure.Email;
using Seed.Infrastructure.Identity;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=seed;Username=seed;Password=seed_dev_password";
        s.AddDbContext<SeedDbContext>(o => o.UseNpgsql(conn));

        s.AddIdentity<ApplicationUser, IdentityRole<Guid>>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<SeedDbContext>()
            .AddDefaultTokenProviders();

        s.AddScoped<ICompanyRepository, CompanyRepository>();
        s.AddScoped<IClock, SystemClock>();
        s.AddScoped<IEmailSender, NoOpEmailSender>();
        s.AddHostedService<AccessControl.PermissionCatalogReconcilerHostedService>();
        s.AddScoped<IEffectivePermissions, AccessControl.EffectivePermissionsService>();
        return s;
    }
}

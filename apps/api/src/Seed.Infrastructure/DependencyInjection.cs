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
        // Deve vir DEPOIS do reconciliador (ordem de registro = ordem de start):
        // o bootstrap concede "todas as permissões ativas" e precisa da tabela
        // Permission já populada. Se o reconciliador virar um BackgroundService
        // (que não bloqueia StartAsync) ou esta linha for movida para antes dele,
        // o "Administrador" seria semeado sem permissões. O teste
        // Demo_org_has_system_admin_profile_with_all_active_permissions guarda
        // esse caso: falha (activeKeys vazio) se essa ordem quebrar.
        s.AddHostedService<AccessControl.AccessControlBootstrapperHostedService>();
        s.AddScoped<IEffectivePermissions, AccessControl.EffectivePermissionsService>();
        s.AddScoped<IPermissionQuery, AccessControl.PermissionQuery>();
        s.AddScoped<Seed.Application.Audit.IAuditLog, Audit.AuditLog>();
        s.AddScoped<IProfileService, AccessControl.ProfileService>();
        s.AddScoped<IUserService, AccessControl.UserService>();
        s.AddScoped<ICompanyAccessService, Companies.CompanyAccessService>();
        return s;
    }
}

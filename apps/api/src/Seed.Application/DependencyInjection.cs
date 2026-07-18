using Microsoft.Extensions.DependencyInjection;

namespace Seed.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.AddScoped<Companies.ICompanyService, Companies.CompanyService>();
        return s;
    }
}

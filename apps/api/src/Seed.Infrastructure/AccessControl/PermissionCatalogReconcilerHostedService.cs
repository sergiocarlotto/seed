using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Seed.Application.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Roda a reconciliação do catálogo no boot, após as migrations (em Development,
// Program.cs migra antes de os hosted services iniciarem; em produção as
// migrations são aplicadas explicitamente antes do deploy — ADR-0007).
public class PermissionCatalogReconcilerHostedService(IServiceProvider sp) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<IPermissionCatalog>();
        await PermissionCatalogReconciler.ReconcileAsync(db, catalog.All, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

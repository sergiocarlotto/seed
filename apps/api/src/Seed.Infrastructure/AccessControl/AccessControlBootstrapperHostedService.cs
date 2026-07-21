using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Roda o bootstrap do "Administrador" no boot. Registrado APÓS o hosted service
// do reconciliador do catálogo (ordem de registro = ordem de start), garantindo
// que a tabela Permission já esteja populada.
public class AccessControlBootstrapperHostedService(IServiceProvider sp) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        await AccessControlBootstrapper.SeedAsync(db, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

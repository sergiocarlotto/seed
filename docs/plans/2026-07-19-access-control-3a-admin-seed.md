# Access Control — Seed do "Administrador" + is_owner (Plano 3a)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development ou superpowers:executing-plans. Steps usam checkbox (`- [ ]`).

**Goal:** Toda organização passa a nascer/ter um perfil de sistema "Administrador" com todas as permissões ativas, e os usuários com `orgRole = Admin` viram `is_owner` vinculados a esse perfil — via um bootstrap idempotente no boot (após o reconciliador do catálogo).

**Architecture:** Um `IHostedService` que roda no startup **depois** do reconciliador do catálogo (que popula a tabela `Permission`). Ele garante, para cada organização, o perfil `is_system` "Administrador" com todas as permissões ativas, e marca os admins como `is_owner` ligando-os ao perfil. Idempotente (roda todo boot sem duplicar). **Não é uma migration EF** — a lógica precisa das permissões já reconciliadas, o que só acontece em runtime; um seeder de runtime é mais portável do que um data-migration que leria o catálogo.

**Tech Stack:** C# / .NET 10, EF Core + PostgreSQL, xUnit + Testcontainers.

**Depende de:** Planos 1 (fundação + reconciliador) e 2 (enforcement) já implementados nesta branch.

**Spec:** `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` · **ADR:** ADR-0012.

**Escopo (o que NÃO entra):** endpoints de perfis/usuários, atribuição, `/auth/me`, emissão de auditoria, troca do gate de `companies`, remoção de `orgRole`, frontend — planos seguintes. Aqui não há mudança de schema (nenhuma migration).

**IMPORTANTE — como rodar testes nesta máquina:** o Smart App Control bloqueia `dotnet test` no host. Use o runner em container: em **PowerShell, na raiz do repo**, `./scripts/test.ps1 [--filter ...]`. O `dotnet build Seed.slnx` continua funcionando no host para checagem de compilação.

---

## File Structure

**Criar:**
- `src/Seed.Infrastructure/AccessControl/AccessControlBootstrapper.cs` — núcleo idempotente (testável).
- `src/Seed.Infrastructure/AccessControl/AccessControlBootstrapperHostedService.cs` — roda no boot.
- `tests/Seed.IntegrationTests/AccessControlBootstrapTests.cs` — testes do resultado do bootstrap.

**Modificar:**
- `src/Seed.Infrastructure/DependencyInjection.cs` — registrar o hosted service **logo após** o do reconciliador (a ordem de registro é a ordem de start).

---

## Task 1: Bootstrapper do "Administrador" + is_owner

**Files:**
- Create: `src/Seed.Infrastructure/AccessControl/AccessControlBootstrapper.cs`
- Create: `src/Seed.Infrastructure/AccessControl/AccessControlBootstrapperHostedService.cs`
- Modify: `src/Seed.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Escrever o núcleo do bootstrapper**

`src/Seed.Infrastructure/AccessControl/AccessControlBootstrapper.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Garante, para cada organização, o perfil de sistema "Administrador" com todas
// as permissões ativas, e marca os usuários orgRole=Admin como owner, ligando-os
// ao perfil. Idempotente: roda todo boot sem duplicar. Deve rodar APÓS o
// reconciliador do catálogo (precisa das permissões já projetadas na tabela).
public static class AccessControlBootstrapper
{
    public const string AdminProfileName = "Administrador";

    public static async Task SeedAsync(SeedDbContext db, CancellationToken ct)
    {
        var activeKeys = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .Select(p => p.Key)
            .ToListAsync(ct);

        var orgIds = await db.Organizations.Select(o => o.Id).ToListAsync(ct);

        foreach (var orgId in orgIds)
        {
            // 1. Garante o perfil de sistema "Administrador" da organização.
            var adminProfile = await db.Profiles
                .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IsSystem, ct);
            if (adminProfile is null)
            {
                var now = DateTime.UtcNow;
                adminProfile = new Profile
                {
                    OrganizationId = orgId,
                    Name = AdminProfileName,
                    Description = "Perfil de sistema com todas as permissões.",
                    IsSystem = true,
                    Status = ProfileStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Profiles.Add(adminProfile);
                await db.SaveChangesAsync(ct); // materializa o Id
            }

            // 2. Top-up: o "Administrador" concede todas as permissões ativas.
            var granted = await db.ProfilePermissions
                .Where(pp => pp.ProfileId == adminProfile.Id)
                .Select(pp => pp.PermissionKey)
                .ToListAsync(ct);
            foreach (var key in activeKeys.Except(granted))
                db.ProfilePermissions.Add(new ProfilePermission
                {
                    ProfileId = adminProfile.Id,
                    PermissionKey = key,
                });

            // 3. Admins da org viram owner e são ligados ao perfil "Administrador".
            var admins = await db.Users
                .Where(u => u.OrganizationId == orgId && u.OrgRole == OrganizationRole.Admin)
                .ToListAsync(ct);
            foreach (var user in admins)
            {
                if (!user.IsOwner) user.IsOwner = true;

                var linked = await db.UserProfiles
                    .AnyAsync(up => up.UserId == user.Id && up.ProfileId == adminProfile.Id, ct);
                if (!linked)
                    db.UserProfiles.Add(new UserProfile
                    {
                        UserId = user.Id,
                        ProfileId = adminProfile.Id,
                    });
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 2: Escrever o hosted service**

`src/Seed.Infrastructure/AccessControl/AccessControlBootstrapperHostedService.cs`:

```csharp
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
```

- [ ] **Step 3: Registrar no DI (após o reconciliador)**

Em `src/Seed.Infrastructure/DependencyInjection.cs`, localize a linha que registra
`PermissionCatalogReconcilerHostedService` e adicione, **imediatamente depois
dela**:

```csharp
        // Deve vir DEPOIS do reconciliador (ordem de registro = ordem de start):
        // o bootstrap concede "todas as permissões ativas" e precisa da tabela
        // Permission já populada.
        s.AddHostedService<AccessControl.AccessControlBootstrapperHostedService>();
```

- [ ] **Step 4: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 5: Commit**

```bash
git add src/Seed.Infrastructure/AccessControl/AccessControlBootstrapper.cs src/Seed.Infrastructure/AccessControl/AccessControlBootstrapperHostedService.cs src/Seed.Infrastructure/DependencyInjection.cs
git commit -m "feat(access-control): bootstrap do perfil Administrador + is_owner no boot"
```

---

## Task 2: Testes do bootstrap

**Files:**
- Create: `tests/Seed.IntegrationTests/AccessControlBootstrapTests.cs`

- [ ] **Step 1: Escrever os testes**

`tests/Seed.IntegrationTests/AccessControlBootstrapTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Bootstrap no boot: perfil "Administrador" (is_system) com todas as permissões
// ativas por organização, e o admin semeado como owner ligado ao perfil.
public class AccessControlBootstrapTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Demo_org_has_system_admin_profile_with_all_active_permissions()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var orgId = await factory.GetDemoOrganizationIdAsync();

        var admin = await db.Profiles
            .FirstOrDefaultAsync(p => p.OrganizationId == orgId && p.IsSystem);
        Assert.NotNull(admin);
        Assert.Equal(AccessControlBootstrapper.AdminProfileName, admin!.Name);
        Assert.Equal(ProfileStatus.Active, admin.Status);

        var granted = await db.ProfilePermissions
            .Where(pp => pp.ProfileId == admin.Id)
            .Select(pp => pp.PermissionKey)
            .ToListAsync();
        var activeKeys = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .Select(p => p.Key)
            .ToListAsync();

        Assert.NotEmpty(activeKeys);
        Assert.Equal(activeKeys.OrderBy(k => k), granted.OrderBy(k => k));
    }

    [Fact]
    public async Task Seeded_admin_is_owner_and_linked_to_admin_profile()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var adminUser = await db.Users.FirstAsync(u => u.Email == ApiFactory.AdminEmail);
        Assert.True(adminUser.IsOwner);

        var adminProfile = await db.Profiles
            .FirstAsync(p => p.OrganizationId == adminUser.OrganizationId && p.IsSystem);
        var linked = await db.UserProfiles
            .AnyAsync(up => up.UserId == adminUser.Id && up.ProfileId == adminProfile.Id);
        Assert.True(linked);
    }
}
```

- [ ] **Step 2: Rodar os testes do bootstrap (runner em container)**

Run (PowerShell, na raiz do repo):
`./scripts/test.ps1 --filter "FullyQualifiedName~AccessControlBootstrapTests"`
Expected: Passed! 2 testes verdes.

- [ ] **Step 3: Rodar a suíte inteira (sem regressão)**

Run (PowerShell, na raiz do repo): `./scripts/test.ps1`
Expected: Passed! Todos verdes (1 unit + 22 integração = 23). `CompaniesTests`
seguem verdes: o gate de empresa ainda é `orgRole` (inalterado); marcar o admin
como `is_owner` não muda o comportamento de empresas.

- [ ] **Step 4: Commit**

```bash
git add tests/Seed.IntegrationTests/AccessControlBootstrapTests.cs
git commit -m "test(access-control): bootstrap do Administrador (perfil + owner + vinculo)"
```

---

## Self-Review

**Cobertura do escopo (3a):**
- Perfil "Administrador" (`is_system`, Active) por organização — Task 1. ✅
- Todas as permissões ativas concedidas ao "Administrador" (com top-up idempotente) — Task 1. ✅
- `orgRole = Admin` → `is_owner = true` + vínculo `UserProfile` — Task 1. ✅
- Roda após o reconciliador (ordem de registro dos hosted services) — Task 1, Step 3. ✅
- Testes do resultado do boot — Task 2. ✅

**Ordem dos hosted services:** o host inicia hosted services sequencialmente na
ordem de registro; por isso o bootstrap é registrado logo após o reconciliador. O
teste `..._with_all_active_permissions` guarda essa dependência: se rodasse antes,
`activeKeys` estaria vazio e o teste falharia (`Assert.NotEmpty`).

**Não muda comportamento existente:** sem migration; `orgRole` intocado; `companies`
inalterado. Marcar o admin semeado como `is_owner` não afeta `CompaniesTests`
(gate de empresa é `orgRole`).

**Idempotência:** perfil só é criado se ausente; permissões só são adicionadas se
faltando; `is_owner` só setado se falso; vínculo só criado se inexistente. Rodar
em todo boot é seguro.

**Placeholders:** nenhum. **Consistência:** `AccessControlBootstrapper.
AdminProfileName` usado no seeder e no teste; `ProfileStatus.Active`/
`PermissionStatus.Active` conforme Plano 1.

**Testes rodam via `scripts/test.ps1` (container), não `dotnet test` no host (SAC).**

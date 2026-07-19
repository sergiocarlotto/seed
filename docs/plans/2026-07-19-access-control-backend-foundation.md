# Access Control — Backend Foundation (Plano 1/4)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Estabelecer a fundação de dados do módulo `AccessControl`: entidades de perfis/permissões, o catálogo de permissões fixo no código, o reconciliador que projeta o catálogo na tabela `Permission` no boot, e a migration que cria as tabelas e adiciona `is_owner` ao usuário.

**Architecture:** Monólito modular em camadas (Domain / Application / Infrastructure / Api), como o módulo `organizations` existente. O catálogo de permissões é declarado no código (fonte de verdade) e projetado numa tabela `Permission` via reconciliação idempotente no startup; a foreign key `ProfilePermission → Permission` é a trava contra permissões inexistentes.

**Tech Stack:** C# / .NET 10, ASP.NET Core, EF Core + PostgreSQL, xUnit + Testcontainers (Postgres real nos testes de integração).

**Spec:** `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md`
**ADR:** `docs/decisions/ADR-0012-configurable-profiles-and-permissions.md`

**Pré-requisitos de ambiente:**
- Docker rodando (Testcontainers sobe Postgres nos testes).
- Ferramenta EF: se `dotnet ef` não existir, instale com
  `dotnet tool install --global dotnet-ef`.
- Todos os comandos abaixo assumem o diretório de trabalho `apps/api`.

**Escopo deste plano (o que NÃO entra):** enforcement/policies, endpoints,
emissão de auditoria, seed do perfil "Administrador", remoção do `orgRole` e
frontend — tudo isso vem nos planos 2–4.

---

## File Structure

**Criar:**
- `src/Seed.Domain/AccessControl/PermissionStatus.cs` — enum `Active`/`Obsolete`.
- `src/Seed.Domain/AccessControl/Permission.cs` — projeção do catálogo (PK `Key`).
- `src/Seed.Domain/AccessControl/ProfileStatus.cs` — enum `Active`/`Archived`.
- `src/Seed.Domain/AccessControl/Profile.cs` — perfil por organização.
- `src/Seed.Domain/AccessControl/ProfilePermission.cs` — M:N Profile↔Permission.
- `src/Seed.Domain/AccessControl/UserProfile.cs` — M:N User↔Profile.
- `src/Seed.Application/AccessControl/PermissionDefinition.cs` — record do catálogo.
- `src/Seed.Application/AccessControl/IPermissionCatalog.cs` — interface do catálogo.
- `src/Seed.Application/AccessControl/AccessControlPermissions.cs` — permissões do módulo.
- `src/Seed.Application/AccessControl/PermissionCatalog.cs` — agrega os módulos.
- `src/Seed.Infrastructure/AccessControl/PermissionCatalogReconciler.cs` — núcleo reconciliador (testável).
- `src/Seed.Infrastructure/AccessControl/PermissionCatalogReconcilerHostedService.cs` — roda no boot.
- `tests/Seed.IntegrationTests/AccessControlCatalogTests.cs` — testes do catálogo/FK.

**Modificar:**
- `src/Seed.Infrastructure/Identity/ApplicationUser.cs` — adicionar `IsOwner`.
- `src/Seed.Infrastructure/Persistence/SeedDbContext.cs` — DbSets + mapeamento.
- `src/Seed.Application/DependencyInjection.cs` — registrar `IPermissionCatalog`.
- `src/Seed.Infrastructure/DependencyInjection.cs` — registrar o hosted service.
- Nova migration em `src/Seed.Infrastructure/Persistence/Migrations/` (gerada pelo EF).

---

## Task 1: Entidades de domínio do AccessControl

**Files:**
- Create: `src/Seed.Domain/AccessControl/PermissionStatus.cs`
- Create: `src/Seed.Domain/AccessControl/Permission.cs`
- Create: `src/Seed.Domain/AccessControl/ProfileStatus.cs`
- Create: `src/Seed.Domain/AccessControl/Profile.cs`
- Create: `src/Seed.Domain/AccessControl/ProfilePermission.cs`
- Create: `src/Seed.Domain/AccessControl/UserProfile.cs`

Entidades são estruturais (sem comportamento); a validação vem via build (Task 1)
e via os testes de integração da Task 5. Sem teste unitário dedicado aqui.

- [ ] **Step 1: Criar `PermissionStatus.cs`**

```csharp
namespace Seed.Domain.AccessControl;

public enum PermissionStatus { Active = 0, Obsolete = 1 }
```

- [ ] **Step 2: Criar `Permission.cs`**

```csharp
namespace Seed.Domain.AccessControl;

// Projeção do catálogo de permissões declarado no código (reconciliada no boot).
// Chave estável como PK; global à instância (sem organization_id).
public class Permission
{
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PermissionStatus Status { get; set; } = PermissionStatus.Active;
}
```

- [ ] **Step 3: Criar `ProfileStatus.cs`**

```csharp
namespace Seed.Domain.AccessControl;

public enum ProfileStatus { Active = 0, Archived = 1 }
```

- [ ] **Step 4: Criar `Profile.cs`**

```csharp
using Seed.Domain.Common;

namespace Seed.Domain.AccessControl;

// Perfil configurável, escopo organização. "Arquivar" usa Status (não exclusão
// física); o soft delete de Entity permanece disponível mas não é o mecanismo
// de arquivamento.
public class Profile : Entity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public ProfileStatus Status { get; set; } = ProfileStatus.Active;
}
```

- [ ] **Step 5: Criar `ProfilePermission.cs`**

```csharp
namespace Seed.Domain.AccessControl;

// M:N Profile <-> Permission. A FK em PermissionKey (configurada no DbContext)
// é a trava contra conceder permissão inexistente.
public class ProfilePermission
{
    public Guid ProfileId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
}
```

- [ ] **Step 6: Criar `UserProfile.cs`**

```csharp
namespace Seed.Domain.AccessControl;

// M:N ApplicationUser <-> Profile. Permissão efetiva = união dos perfis ativos.
public class UserProfile
{
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
}
```

- [ ] **Step 7: Build para verificar que compila**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 8: Commit**

```bash
git add src/Seed.Domain/AccessControl
git commit -m "feat(access-control): entidades de dominio (Permission, Profile, vinculos)"
```

---

## Task 2: Catálogo de permissões (Application)

**Files:**
- Create: `src/Seed.Application/AccessControl/PermissionDefinition.cs`
- Create: `src/Seed.Application/AccessControl/IPermissionCatalog.cs`
- Create: `src/Seed.Application/AccessControl/AccessControlPermissions.cs`
- Create: `src/Seed.Application/AccessControl/PermissionCatalog.cs`
- Modify: `src/Seed.Application/DependencyInjection.cs`

- [ ] **Step 1: Criar `PermissionDefinition.cs`**

```csharp
namespace Seed.Application.AccessControl;

// Declaração de uma permissão no catálogo do código (fonte de verdade).
public record PermissionDefinition(string Key, string Module, string DisplayName, string Description);
```

- [ ] **Step 2: Criar `IPermissionCatalog.cs`**

```csharp
namespace Seed.Application.AccessControl;

// Agrega as permissões declaradas por todos os módulos. Fonte de verdade do
// catálogo; a tabela Permission é apenas a projeção reconciliada no boot.
public interface IPermissionCatalog
{
    IReadOnlyList<PermissionDefinition> All { get; }
}
```

- [ ] **Step 3: Criar `AccessControlPermissions.cs`**

```csharp
namespace Seed.Application.AccessControl;

// Permissões declaradas pelo módulo AccessControl. Chaves estáveis e imutáveis
// (renomear = obsoletar a antiga e criar nova).
public static class AccessControlPermissions
{
    public const string Module = "access_control";

    public const string ProfilesManage = "profiles.manage";
    public const string ProfilesAssign = "profiles.assign";
    public const string UsersManage = "users.manage";

    public static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        new(ProfilesManage, Module, "Gerir perfis",
            "Criar, editar e arquivar perfis e definir suas permissões."),
        new(ProfilesAssign, Module, "Atribuir perfis",
            "Atribuir e remover perfis dos usuários."),
        new(UsersManage, Module, "Gerir usuários",
            "Listar, ativar e desativar usuários."),
    ];
}
```

- [ ] **Step 4: Criar `PermissionCatalog.cs`**

```csharp
namespace Seed.Application.AccessControl;

// Junta as declarações de todos os módulos. Ao adicionar um módulo novo com
// permissões, concatene suas Definitions aqui (ex.: CompaniesPermissions no
// Plano 2).
public class PermissionCatalog : IPermissionCatalog
{
    public IReadOnlyList<PermissionDefinition> All { get; } =
        [.. AccessControlPermissions.Definitions];
}
```

- [ ] **Step 5: Registrar o catálogo na DI**

Modificar `src/Seed.Application/DependencyInjection.cs` — adicionar o registro
como singleton (o catálogo é imutável):

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Seed.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.AddScoped<Companies.ICompanyService, Companies.CompanyService>();
        s.AddSingleton<AccessControl.IPermissionCatalog, AccessControl.PermissionCatalog>();
        return s;
    }
}
```

- [ ] **Step 6: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 7: Commit**

```bash
git add src/Seed.Application/AccessControl src/Seed.Application/DependencyInjection.cs
git commit -m "feat(access-control): catalogo de permissoes fixo no codigo"
```

---

## Task 3: `IsOwner` no usuário + mapeamento EF

**Files:**
- Modify: `src/Seed.Infrastructure/Identity/ApplicationUser.cs`
- Modify: `src/Seed.Infrastructure/Persistence/SeedDbContext.cs`

- [ ] **Step 1: Adicionar `IsOwner` ao `ApplicationUser`**

Arquivo completo `src/Seed.Infrastructure/Identity/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public OrganizationRole OrgRole { get; set; } = OrganizationRole.Member;

    // Dono da organização. Gerido fora da aplicação (banco/superadmin externo);
    // nunca setado via API. Tem bypass funcional; é somente-leitura na gestão.
    public bool IsOwner { get; set; }
}
```

- [ ] **Step 2: Mapear as entidades no `SeedDbContext`**

Arquivo completo `src/Seed.Infrastructure/Persistence/SeedDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Seed.Domain.Access;
using Seed.Domain.AccessControl;
using Seed.Domain.Audit;
using Seed.Domain.Companies;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Identity;

namespace Seed.Infrastructure.Persistence;

public class SeedDbContext(DbContextOptions<SeedDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserCompanyAccess> UserCompanyAccesses => Set<UserCompanyAccess>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfilePermission> ProfilePermissions => Set<ProfilePermission>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.Property(o => o.Name).IsRequired().HasMaxLength(200);
            e.HasQueryFilter(o => o.DeletedAt == null);
        });

        builder.Entity<Company>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.OrganizationId);
            e.HasQueryFilter(c => c.DeletedAt == null);
        });

        builder.Entity<UserCompanyAccess>(e =>
        {
            e.HasIndex(a => new { a.UserId, a.CompanyId }).IsUnique();
            e.HasIndex(a => a.UserId);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.Property(a => a.Action).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        });

        builder.Entity<Permission>(e =>
        {
            e.HasKey(p => p.Key);
            e.Property(p => p.Key).HasMaxLength(100);
            e.Property(p => p.Module).IsRequired().HasMaxLength(100);
            e.Property(p => p.DisplayName).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(500);
        });

        builder.Entity<Profile>(e =>
        {
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(500);
            e.HasIndex(p => new { p.OrganizationId, p.Name }).IsUnique();
            e.HasQueryFilter(p => p.DeletedAt == null);
        });

        builder.Entity<ProfilePermission>(e =>
        {
            e.HasKey(pp => new { pp.ProfileId, pp.PermissionKey });
            e.HasOne<Profile>().WithMany()
                .HasForeignKey(pp => pp.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Permission>().WithMany()
                .HasForeignKey(pp => pp.PermissionKey).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserProfile>(e =>
        {
            e.HasKey(up => new { up.UserId, up.ProfileId });
            e.HasOne<Profile>().WithMany()
                .HasForeignKey(up => up.ProfileId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(up => up.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add src/Seed.Infrastructure/Identity/ApplicationUser.cs src/Seed.Infrastructure/Persistence/SeedDbContext.cs
git commit -m "feat(access-control): mapeamento EF e coluna is_owner"
```

---

## Task 4: Migration `AddAccessControl`

**Files:**
- Create (gerado pelo EF): `src/Seed.Infrastructure/Persistence/Migrations/*_AddAccessControl.cs` (+ Designer + snapshot atualizado).

- [ ] **Step 1: Gerar a migration**

Run (em `apps/api`):
```bash
dotnet ef migrations add AddAccessControl \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api
```
Expected: cria os arquivos `..._AddAccessControl.cs` e `.Designer.cs` e atualiza
`SeedDbContextModelSnapshot.cs`. "Done."

- [ ] **Step 2: Revisar a migration gerada**

Abrir o `..._AddAccessControl.cs` e confirmar que o `Up`:
- cria as tabelas `Permissions` (PK `Key`), `Profiles`, `ProfilePermissions`,
  `UserProfiles`;
- adiciona a coluna `IsOwner` (bool, NOT NULL) em `AspNetUsers`;
- cria a FK de `ProfilePermissions.PermissionKey` → `Permissions.Key` com
  `onDelete: Restrict`;
- cria o índice único em `Profiles (OrganizationId, Name)`.

Se a coluna `IsOwner` vier sem default no banco existente, edite o
`migrationBuilder.AddColumn<bool>(... "IsOwner" ...)` para incluir
`defaultValue: false`.

- [ ] **Step 3: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add src/Seed.Infrastructure/Persistence/Migrations
git commit -m "feat(access-control): migration AddAccessControl (tabelas + is_owner)"
```

---

## Task 5: Reconciliador do catálogo + testes

**Files:**
- Create: `src/Seed.Infrastructure/AccessControl/PermissionCatalogReconciler.cs`
- Create: `src/Seed.Infrastructure/AccessControl/PermissionCatalogReconcilerHostedService.cs`
- Modify: `src/Seed.Infrastructure/DependencyInjection.cs`
- Create: `tests/Seed.IntegrationTests/AccessControlCatalogTests.cs`

- [ ] **Step 1: Escrever o núcleo do reconciliador**

`src/Seed.Infrastructure/AccessControl/PermissionCatalogReconciler.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Reconcilia o catálogo do código (fonte de verdade) na tabela Permission.
// Idempotente: insere novas, atualiza metadados, reativa as que reaparecem e
// marca como Obsolete as que sumiram do código. Núcleo separado do hosted
// service para ser testável com catálogos arbitrários.
public static class PermissionCatalogReconciler
{
    public static async Task ReconcileAsync(
        SeedDbContext db,
        IReadOnlyList<PermissionDefinition> defs,
        CancellationToken ct)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, ct);
        var declared = defs.Select(d => d.Key).ToHashSet();

        foreach (var d in defs)
        {
            if (existing.TryGetValue(d.Key, out var p))
            {
                p.Module = d.Module;
                p.DisplayName = d.DisplayName;
                p.Description = d.Description;
                p.Status = PermissionStatus.Active; // reativa se estava Obsolete
            }
            else
            {
                db.Permissions.Add(new Permission
                {
                    Key = d.Key,
                    Module = d.Module,
                    DisplayName = d.DisplayName,
                    Description = d.Description,
                    Status = PermissionStatus.Active,
                });
            }
        }

        foreach (var (key, p) in existing)
            if (!declared.Contains(key))
                p.Status = PermissionStatus.Obsolete;

        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Escrever o hosted service**

`src/Seed.Infrastructure/AccessControl/PermissionCatalogReconcilerHostedService.cs`:

```csharp
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
```

- [ ] **Step 3: Registrar o hosted service na DI**

Modificar `src/Seed.Infrastructure/DependencyInjection.cs` — adicionar, ao final
do corpo de `AddInfrastructure`, antes do `return s;`:

```csharp
        s.AddHostedService<AccessControl.PermissionCatalogReconcilerHostedService>();
```

(Manter os `using` existentes; o namespace `Seed.Infrastructure.AccessControl` é
resolvido pelo prefixo `AccessControl.` dentro de `Seed.Infrastructure`.)

- [ ] **Step 4: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 5: Escrever os testes de integração (que devem falhar antes do fim)**

`tests/Seed.IntegrationTests/AccessControlCatalogTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Catálogo de permissões: reconciliação no boot, trava de FK e ciclo
// obsolete/reativação do reconciliador.
public class AccessControlCatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Catalog_is_reconciled_on_startup()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var keys = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .Select(p => p.Key)
            .ToListAsync();

        Assert.Contains(AccessControlPermissions.ProfilesManage, keys);
        Assert.Contains(AccessControlPermissions.ProfilesAssign, keys);
        Assert.Contains(AccessControlPermissions.UsersManage, keys);
    }

    [Fact]
    public async Task Fk_rejects_unknown_permission_key()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var orgId = await factory.GetDemoOrganizationIdAsync();
        var now = DateTime.UtcNow;
        var profile = new Profile
        {
            OrganizationId = orgId, Name = "Perfil FK Teste",
            CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();

        db.ProfilePermissions.Add(new ProfilePermission
        {
            ProfileId = profile.Id, PermissionKey = "does.not.exist",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Reconciler_marks_missing_as_obsolete_and_reactivates()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var withTemp = new List<PermissionDefinition>(AccessControlPermissions.Definitions)
        {
            new("temp.key", "temp", "Temp", "Permissão temporária de teste"),
        };

        // 1) temp.key entra como Active.
        await PermissionCatalogReconciler.ReconcileAsync(db, withTemp, default);
        var temp = await db.Permissions.SingleAsync(p => p.Key == "temp.key");
        Assert.Equal(PermissionStatus.Active, temp.Status);

        // 2) some do catálogo → Obsolete.
        await PermissionCatalogReconciler.ReconcileAsync(db, AccessControlPermissions.Definitions, default);
        await db.Entry(temp).ReloadAsync();
        Assert.Equal(PermissionStatus.Obsolete, temp.Status);

        // 3) reaparece → volta a Active.
        await PermissionCatalogReconciler.ReconcileAsync(db, withTemp, default);
        await db.Entry(temp).ReloadAsync();
        Assert.Equal(PermissionStatus.Active, temp.Status);
    }
}
```

- [ ] **Step 6: Rodar os testes e confirmar que passam**

Run (em `apps/api`, com Docker ativo):
```bash
dotnet test Seed.slnx --filter "FullyQualifiedName~AccessControlCatalogTests"
```
Expected: Passed! 3 testes verdes (`Catalog_is_reconciled_on_startup`,
`Fk_rejects_unknown_permission_key`,
`Reconciler_marks_missing_as_obsolete_and_reactivates`).

- [ ] **Step 7: Rodar a suíte inteira para garantir que nada regrediu**

Run: `dotnet test Seed.slnx`
Expected: Passed! Todos os testes (inclusive `CompaniesTests`, `AuthTests`)
verdes.

- [ ] **Step 8: Commit**

```bash
git add src/Seed.Infrastructure/AccessControl src/Seed.Infrastructure/DependencyInjection.cs tests/Seed.IntegrationTests/AccessControlCatalogTests.cs
git commit -m "feat(access-control): reconciliador do catalogo no boot + testes"
```

---

## Self-Review

**Cobertura do escopo deste plano (fundação):**
- Entidades `Permission`/`Profile`/`ProfilePermission`/`UserProfile` — Task 1. ✅
- `is_owner` no usuário — Task 3. ✅
- Catálogo fixo no código + `IPermissionCatalog` — Task 2. ✅
- Reconciliador idempotente (insere/atualiza/obsoleta/reativa) no boot — Task 5. ✅
- FK como trava contra permissão inexistente — Task 3 (mapeamento) + Task 5 (teste). ✅
- Migration criando tabelas + `is_owner` — Task 4. ✅
- Índice único `(OrganizationId, Name)` de perfil — Task 3. ✅

**Fora deste plano (planos 2–4), intencionalmente:** enforcement/policies,
resolução de permissão efetiva, endpoints, `/auth/me`, emissão de auditoria,
perfil-semente "Administrador", `companies.access/manage`, remoção do `orgRole`,
frontend.

**Placeholders:** nenhum — todo passo tem código/comando concreto (a migration é
gerada pelo EF, com checklist de revisão explícito na Task 4).

**Consistência de tipos:** `PermissionDefinition(Key, Module, DisplayName,
Description)` é usado igual no catálogo (Task 2), no reconciliador (Task 5) e nos
testes (Task 5). `PermissionStatus.Active/Obsolete` e `ProfileStatus.Active/
Archived` consistentes. `IPermissionCatalog.All` idem. Assinatura
`ReconcileAsync(SeedDbContext, IReadOnlyList<PermissionDefinition>,
CancellationToken)` idêntica na definição e nas chamadas dos testes.

**Observação de execução:** rode numa branch de feature (ex.: worktree
`feat/access-control`), não direto na `main`.

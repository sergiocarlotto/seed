# Módulo `organizations` — Login + CRUD de Empresa — Plano de Implementação

> **Para executores agênticos:** use este plano tarefa a tarefa, em ordem. Os
> passos usam `- [ ]` para acompanhamento. Execução **autônoma**: não pare para
> pedir decisões ao usuário — siga sempre a opção recomendada registrada aqui.

**Goal:** Entregar, ponta a ponta, a base de tenancy do Seed com cadastro/login
por email+senha (cookie httpOnly) e um CRUD básico de empresa (`Organization`),
com isolamento por tenant garantido no backend e telas mínimas no frontend.

**Architecture:** Monólito modular ASP.NET Core (.NET 10) em camadas
(Api/Application/Domain/Infrastructure), PostgreSQL via EF Core + Npgsql,
autenticação com ASP.NET Core Identity (cookie). Frontend Next.js 16 chamando a
API na mesma origem (`/api`, roteado pelo Caddy). Testes de integração com
Testcontainers (Postgres real).

**Tech Stack:** C#/.NET 10, ASP.NET Core Identity, EF Core 10 + Npgsql,
xUnit + Testcontainers.PostgreSql, Next.js 16 + TypeScript + Tailwind.

---

## Decisões travadas (recomendado, sem perguntar)

- **"Empresa" = `Organization`** (o tenant). O CRUD gerencia as organizações das
  quais o usuário logado é membro; criar uma o torna `owner`.
- **Auth**: Identity, email+senha, cookie httpOnly/SameSite=Lax/Secure(em prod).
  `register` cria organização + usuário owner e já autentica. **Convite e
  recuperação de senha ficam de fora** (dependem de email transacional).
- **Papéis**: `owner` > `admin` > `member`. Criador vira `owner`.
- **Soft delete** de organização (marca `DeletedAt`), nunca exclusão física
  (ADR-0005).
- **Migração**: aplicada no boot **apenas** em `Development` (self-contained para
  o Docker de dev); em produção continua passo explícito (ADR-0007).
- **Enforcement no backend**; frontend nunca é barreira de segurança.

## Contrato de API

Rotas do backend (o Caddy remove o prefixo `/api`, então o frontend chama
`/api/...` e o controller responde em `/...`). Todas as respostas em JSON.

| Método | Rota (frontend) | Auth | Corpo | Resposta |
| --- | --- | --- | --- | --- |
| POST | `/api/auth/register` | não | `{ organizationName, fullName, email, password }` | 200 `{ user, organization }` + cookie |
| POST | `/api/auth/login` | não | `{ email, password }` | 200 `{ user }` + cookie / 401 |
| POST | `/api/auth/logout` | sim | — | 204 |
| GET | `/api/auth/me` | sim | — | 200 `{ user, memberships[] }` / 401 |
| GET | `/api/organizations` | sim | — | 200 `Organization[]` (só as do usuário) |
| POST | `/api/organizations` | sim | `{ name }` | 201 `Organization` (usuário vira owner) |
| GET | `/api/organizations/{id}` | sim (membro) | — | 200 `Organization` / 403 / 404 |
| PUT | `/api/organizations/{id}` | sim (owner/admin) | `{ name }` | 200 `Organization` / 403 |
| DELETE | `/api/organizations/{id}` | sim (owner) | — | 204 / 403 |

DTOs:
- `UserDto { id: guid, email: string, fullName: string }`
- `OrganizationDto { id: guid, name: string, status: string, role: string, createdAt, updatedAt }` (`role` = papel do usuário atual naquela org)
- `MembershipDto { organizationId: guid, organizationName: string, role: string }`

Erros: 400 (validação), 401 (não autenticado), 403 (sem permissão / não membro),
404 (não existe). 403 e 404 não devem vazar existência de recursos de outra org
(usar 404 para "não membro" ao acessar por id).

## Estrutura de arquivos

Backend (`apps/api`):
- `src/Seed.Domain/Organizations/Organization.cs` — entidade tenant.
- `src/Seed.Domain/Organizations/OrganizationMembership.cs` — vínculo usuário↔org.
- `src/Seed.Domain/Organizations/OrganizationRole.cs` — enum owner/admin/member.
- `src/Seed.Domain/Organizations/OrganizationStatus.cs` — enum active/suspended.
- `src/Seed.Domain/Memberships/MembershipStatus.cs` — enum active/invited/disabled.
- `src/Seed.Domain/Audit/AuditEvent.cs` — evento de auditoria (ADR-0005).
- `src/Seed.Domain/Common/Entity.cs` — base com Id/CreatedAt/UpdatedAt/DeletedAt.
- `src/Seed.Infrastructure/Identity/ApplicationUser.cs` — `IdentityUser<Guid>` + FullName.
- `src/Seed.Infrastructure/Persistence/SeedDbContext.cs` — `IdentityDbContext` + DbSets + config.
- `src/Seed.Infrastructure/Persistence/Migrations/*` — migration InitialCreate (gerada).
- `src/Seed.Infrastructure/Persistence/SeedDbContextFactory.cs` — design-time factory.
- `src/Seed.Infrastructure/Email/IEmailSender.cs` + `NoOpEmailSender.cs` — stub (futuro).
- `src/Seed.Application/Abstractions/ICurrentUser.cs` — usuário autenticado atual.
- `src/Seed.Application/Abstractions/IClock.cs` — relógio (UTC).
- `src/Seed.Application/Organizations/OrganizationService.cs` + `IOrganizationService.cs` — casos de uso + regras de tenant.
- `src/Seed.Application/Organizations/Dtos.cs` — DTOs de request/response.
- `src/Seed.Application/DependencyInjection.cs` — registro dos serviços da Application.
- `src/Seed.Infrastructure/DependencyInjection.cs` — registro de DbContext/Identity/serviços.
- `src/Seed.Api/Controllers/AuthController.cs` — register/login/logout/me.
- `src/Seed.Api/Controllers/OrganizationsController.cs` — CRUD.
- `src/Seed.Api/CurrentUser.cs` — `ICurrentUser` a partir do `HttpContext`.
- `src/Seed.Api/Program.cs` — wiring (DbContext, Identity/cookie, authz, migrate dev).
- `src/Seed.Api/appsettings.Development.json` — connection string default (dev/design-time).
- `tests/Seed.IntegrationTests/ApiFactory.cs` — WebApplicationFactory + Testcontainers Postgres.
- `tests/Seed.IntegrationTests/AuthTests.cs` — register/login/me.
- `tests/Seed.IntegrationTests/OrganizationsTests.cs` — CRUD + cross-tenant.
- `tests/Seed.UnitTests/OrganizationServiceTests.cs` — regras de papel/tenant.

Frontend (`apps/web`):
- `src/lib/api.ts` — cliente fetch (`credentials: 'include'`, base `/api`).
- `src/lib/auth.ts` — helpers de sessão (getMe).
- `src/app/login/page.tsx` — tela de login.
- `src/app/register/page.tsx` — tela de cadastro.
- `src/app/companies/page.tsx` — lista de empresas.
- `src/app/companies/new/page.tsx` — criar empresa.
- `src/app/companies/[id]/page.tsx` — editar/excluir empresa.
- `src/components/*` — formulário e itens de lista reutilizáveis.
- `src/middleware.ts` — protege `/companies*` (redireciona para `/login` sem sessão).
- `apps/web/next.config.ts` — rewrite `/api/*` → `http://localhost:8080/*` só em dev.

## Ambiente para o executor

- `dotnet` está em `C:\Program Files\dotnet`. Antes de comandos .NET/npm em novo
  shell PowerShell, rode:
  `$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")`
- Solution: `apps/api/Seed.slnx`. Framework: `net10.0`.
- Ferramenta de migrations: instalar `dotnet tool install --global dotnet-ef` (se faltar).
- Docker disponível (para Testcontainers e para o e2e).

---

## Task 1: Entidades de domínio e enums

**Files:**
- Create: `apps/api/src/Seed.Domain/Common/Entity.cs`
- Create: `apps/api/src/Seed.Domain/Organizations/OrganizationRole.cs`, `OrganizationStatus.cs`, `Organization.cs`, `OrganizationMembership.cs`
- Create: `apps/api/src/Seed.Domain/Memberships/MembershipStatus.cs`
- Create: `apps/api/src/Seed.Domain/Audit/AuditEvent.cs`
- Remove: `apps/api/src/Seed.Domain/Class1.cs`

- [ ] **Step 1: Base `Entity`**

```csharp
namespace Seed.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted => DeletedAt is not null;
}
```

- [ ] **Step 2: Enums**

```csharp
namespace Seed.Domain.Organizations;
public enum OrganizationRole { Owner = 0, Admin = 1, Member = 2 }
```
```csharp
namespace Seed.Domain.Organizations;
public enum OrganizationStatus { Active = 0, Suspended = 1 }
```
```csharp
namespace Seed.Domain.Memberships;
public enum MembershipStatus { Active = 0, Invited = 1, Disabled = 2 }
```

- [ ] **Step 3: `Organization` e `OrganizationMembership`**

```csharp
using Seed.Domain.Common;

namespace Seed.Domain.Organizations;

public class Organization : Entity
{
    public string Name { get; set; } = string.Empty;
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();
}
```
```csharp
using Seed.Domain.Common;
using Seed.Domain.Memberships;

namespace Seed.Domain.Organizations;

public class OrganizationMembership : Entity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid UserId { get; set; }
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
}
```

- [ ] **Step 4: `AuditEvent`** (mínimo da ADR-0005)

```csharp
namespace Seed.Domain.Audit;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? Metadata { get; set; }
}
```

- [ ] **Step 5: build e commit**

Run: `dotnet build apps/api/src/Seed.Domain/Seed.Domain.csproj`
Expected: sucesso, 0 erros.
```bash
git add apps/api/src/Seed.Domain
git commit -m "feat(domain): entidades Organization, Membership e AuditEvent"
```

---

## Task 2: Persistência (EF Core + Identity + Postgres)

**Files:**
- Create: `apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs`
- Create: `apps/api/src/Seed.Infrastructure/Persistence/SeedDbContext.cs`
- Create: `apps/api/src/Seed.Infrastructure/Persistence/SeedDbContextFactory.cs`
- Create: `apps/api/src/Seed.Infrastructure/Email/IEmailSender.cs`, `NoOpEmailSender.cs`
- Remove: `apps/api/src/Seed.Infrastructure/Class1.cs`
- Modify: `apps/api/src/Seed.Infrastructure/Seed.Infrastructure.csproj` (packages)

- [ ] **Step 1: pacotes**

Run:
```
dotnet add apps/api/src/Seed.Infrastructure package Microsoft.EntityFrameworkCore
dotnet add apps/api/src/Seed.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add apps/api/src/Seed.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add apps/api/src/Seed.Infrastructure package Microsoft.EntityFrameworkCore.Design
```
> Se algum pacote acusar vulnerabilidade (NU1903), fixe a versão estável mais
> recente compatível (mesma abordagem do scaffold com Microsoft.OpenApi).

- [ ] **Step 2: `ApplicationUser`**

```csharp
using Microsoft.AspNetCore.Identity;

namespace Seed.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
}
```

- [ ] **Step 3: `SeedDbContext`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Seed.Domain.Audit;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Identity;

namespace Seed.Infrastructure.Persistence;

public class SeedDbContext(DbContextOptions<SeedDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.Property(o => o.Name).IsRequired().HasMaxLength(200);
            e.HasQueryFilter(o => o.DeletedAt == null);
        });

        builder.Entity<OrganizationMembership>(e =>
        {
            e.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
            e.HasOne(m => m.Organization)
                .WithMany(o => o.Memberships)
                .HasForeignKey(m => m.OrganizationId);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.Property(a => a.Action).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        });
    }
}
```

- [ ] **Step 4: design-time factory** (permite `dotnet ef` sem subir a app)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Seed.Infrastructure.Persistence;

public class SeedDbContextFactory : IDesignTimeDbContextFactory<SeedDbContext>
{
    public SeedDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=seed;Username=seed;Password=seed_dev_password";
        var options = new DbContextOptionsBuilder<SeedDbContext>()
            .UseNpgsql(conn).Options;
        return new SeedDbContext(options);
    }
}
```

- [ ] **Step 5: stub de email**

```csharp
namespace Seed.Infrastructure.Email;
public interface IEmailSender { Task SendAsync(string to, string subject, string body); }
```
```csharp
using Microsoft.Extensions.Logging;
namespace Seed.Infrastructure.Email;
public class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        logger.LogInformation("Email (stub) para {To}: {Subject}", to, subject);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 6: build**

Run: `dotnet build apps/api/src/Seed.Infrastructure/Seed.Infrastructure.csproj`
Expected: sucesso.

- [ ] **Step 7: commit**
```bash
git add apps/api/src/Seed.Infrastructure
git commit -m "feat(infra): DbContext, ApplicationUser (Identity) e stub de email"
```

---

## Task 3: Camada Application (abstrações, DTOs, serviço)

**Files:**
- Create: `apps/api/src/Seed.Application/Abstractions/ICurrentUser.cs`, `IClock.cs`
- Create: `apps/api/src/Seed.Application/Organizations/Dtos.cs`
- Create: `apps/api/src/Seed.Application/Organizations/IOrganizationService.cs`, `OrganizationService.cs`
- Create: `apps/api/src/Seed.Application/DependencyInjection.cs`
- Remove: `apps/api/src/Seed.Application/Class1.cs`
- Modify: `Seed.Application.csproj` — add package `Microsoft.EntityFrameworkCore` (para consultas via DbContext abstrato? Não: manter Application sem EF; o serviço recebe DbContext concreto? Ver nota)

> **Nota de arquitetura (recomendado):** para manter simples e ainda respeitar
> camadas, o `OrganizationService` fica na Application e depende de uma
> abstração de dados. Neste MVP, definimos a interface `IOrganizationRepository`
> na Application e a implementamos na Infrastructure sobre o `SeedDbContext`.
> Assim a Application não referencia EF Core.

Ajuste de Files:
- Create: `apps/api/src/Seed.Application/Organizations/IOrganizationRepository.cs`
- Create: `apps/api/src/Seed.Infrastructure/Persistence/OrganizationRepository.cs`

- [ ] **Step 1: abstrações**

```csharp
namespace Seed.Application.Abstractions;
public interface ICurrentUser { Guid? UserId { get; } bool IsAuthenticated { get; } }
```
```csharp
namespace Seed.Application.Abstractions;
public interface IClock { DateTime UtcNow { get; } }
```

- [ ] **Step 2: DTOs**

```csharp
namespace Seed.Application.Organizations;

public record CreateOrganizationRequest(string Name);
public record UpdateOrganizationRequest(string Name);
public record OrganizationDto(Guid Id, string Name, string Status, string Role, DateTime CreatedAt, DateTime UpdatedAt);
public record MembershipDto(Guid OrganizationId, string OrganizationName, string Role);
```

- [ ] **Step 3: repositório (interface na Application)**

```csharp
using Seed.Domain.Organizations;
namespace Seed.Application.Organizations;

public interface IOrganizationRepository
{
    Task<List<(Organization Org, OrganizationRole Role)>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<Organization?> GetByIdForUserAsync(Guid orgId, Guid userId, CancellationToken ct);
    Task<OrganizationRole?> GetRoleAsync(Guid orgId, Guid userId, CancellationToken ct);
    Task AddAsync(Organization org, OrganizationMembership ownerMembership, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

- [ ] **Step 4: `IOrganizationService` + implementação**

Regras:
- `CreateAsync(name)`: cria org + membership Owner do usuário atual; grava AuditEvent `organization.created`.
- `ListAsync()`: orgs do usuário atual (com o papel).
- `GetAsync(id)`: exige membership (senão devolve null → 404).
- `UpdateAsync(id, name)`: exige role Owner ou Admin (senão `ForbiddenException`).
- `DeleteAsync(id)`: exige role Owner; soft delete; AuditEvent `organization.deleted`.

```csharp
using Seed.Application.Abstractions;
using Seed.Domain.Organizations;
using Seed.Domain.Memberships;

namespace Seed.Application.Organizations;

public class ForbiddenException(string message) : Exception(message);

public interface IOrganizationService
{
    Task<List<OrganizationDto>> ListAsync(CancellationToken ct);
    Task<OrganizationDto?> GetAsync(Guid id, CancellationToken ct);
    Task<OrganizationDto> CreateAsync(CreateOrganizationRequest req, CancellationToken ct);
    Task<OrganizationDto?> UpdateAsync(Guid id, UpdateOrganizationRequest req, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

public class OrganizationService(
    IOrganizationRepository repo,
    ICurrentUser currentUser,
    IClock clock) : IOrganizationService
{
    private Guid UserId => currentUser.UserId ?? throw new ForbiddenException("Não autenticado.");

    public async Task<List<OrganizationDto>> ListAsync(CancellationToken ct)
    {
        var items = await repo.ListForUserAsync(UserId, ct);
        return items.Select(i => Map(i.Org, i.Role)).ToList();
    }

    public async Task<OrganizationDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var role = await repo.GetRoleAsync(id, UserId, ct);
        if (role is null) return null;
        var org = await repo.GetByIdForUserAsync(id, UserId, ct);
        return org is null ? null : Map(org, role.Value);
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest req, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var org = new Organization { Name = req.Name.Trim(), CreatedAt = now, UpdatedAt = now };
        var membership = new OrganizationMembership
        {
            OrganizationId = org.Id, UserId = UserId,
            Role = OrganizationRole.Owner, Status = MembershipStatus.Active,
            CreatedAt = now, UpdatedAt = now
        };
        await repo.AddAsync(org, membership, ct);
        await repo.SaveChangesAsync(ct);
        return Map(org, OrganizationRole.Owner);
    }

    public async Task<OrganizationDto?> UpdateAsync(Guid id, UpdateOrganizationRequest req, CancellationToken ct)
    {
        var role = await repo.GetRoleAsync(id, UserId, ct);
        if (role is null) return null;
        if (role is not (OrganizationRole.Owner or OrganizationRole.Admin))
            throw new ForbiddenException("Sem permissão para editar.");
        var org = await repo.GetByIdForUserAsync(id, UserId, ct);
        if (org is null) return null;
        org.Name = req.Name.Trim();
        org.UpdatedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return Map(org, role.Value);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await repo.GetRoleAsync(id, UserId, ct);
        if (role is null) return false;
        if (role is not OrganizationRole.Owner)
            throw new ForbiddenException("Apenas o owner pode excluir.");
        var org = await repo.GetByIdForUserAsync(id, UserId, ct);
        if (org is null) return false;
        org.DeletedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return true;
    }

    private static OrganizationDto Map(Organization o, OrganizationRole role) =>
        new(o.Id, o.Name, o.Status.ToString(), role.ToString(), o.CreatedAt, o.UpdatedAt);
}
```

- [ ] **Step 5: DI da Application**

```csharp
using Microsoft.Extensions.DependencyInjection;
namespace Seed.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.AddScoped<Organizations.IOrganizationService, Organizations.OrganizationService>();
        return s;
    }
}
```

- [ ] **Step 6: implementar `IOrganizationRepository` na Infrastructure**

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Application.Organizations;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Persistence;

public class OrganizationRepository(SeedDbContext db) : IOrganizationRepository
{
    public async Task<List<(Organization Org, OrganizationRole Role)>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var q = from m in db.Memberships
                join o in db.Organizations on m.OrganizationId equals o.Id
                where m.UserId == userId
                select new { o, m.Role };
        return (await q.ToListAsync(ct)).Select(x => (x.o, x.Role)).ToList();
    }

    public Task<Organization?> GetByIdForUserAsync(Guid orgId, Guid userId, CancellationToken ct) =>
        (from o in db.Organizations
         where o.Id == orgId && db.Memberships.Any(m => m.OrganizationId == orgId && m.UserId == userId)
         select o).FirstOrDefaultAsync(ct);

    public async Task<OrganizationRole?> GetRoleAsync(Guid orgId, Guid userId, CancellationToken ct)
    {
        var m = await db.Memberships.FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.UserId == userId, ct);
        return m?.Role;
    }

    public async Task AddAsync(Organization org, OrganizationMembership ownerMembership, CancellationToken ct)
    {
        await db.Organizations.AddAsync(org, ct);
        await db.Memberships.AddAsync(ownerMembership, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

- [ ] **Step 7: build + commit**

Run: `dotnet build apps/api/Seed.slnx`
Expected: sucesso.
```bash
git add apps/api/src/Seed.Application apps/api/src/Seed.Infrastructure
git commit -m "feat(app): OrganizationService, DTOs e repositório de organizações"
```

---

## Task 4: Wiring da API (Identity/cookie, DbContext, DI, migrate dev)

**Files:**
- Create: `apps/api/src/Seed.Api/CurrentUser.cs`
- Create: `apps/api/src/Seed.Infrastructure/DependencyInjection.cs`
- Create: `apps/api/src/Seed.Application/SystemClock.cs` (ou em Infrastructure)
- Modify: `apps/api/src/Seed.Api/Program.cs`
- Modify: `apps/api/src/Seed.Api/appsettings.Development.json`
- Modify: `Seed.Api.csproj` — add `Microsoft.EntityFrameworkCore.Design`

- [ ] **Step 1: `ICurrentUser` a partir do HttpContext**

```csharp
using System.Security.Claims;
using Seed.Application.Abstractions;

namespace Seed.Api;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var id = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(id, out var g) ? g : null;
        }
    }
    public bool IsAuthenticated => UserId is not null;
}
```

- [ ] **Step 2: `IClock`**

```csharp
using Seed.Application.Abstractions;
namespace Seed.Infrastructure;
public class SystemClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
```

- [ ] **Step 3: DI da Infrastructure (DbContext, Identity, repos, clock, email)**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Abstractions;
using Seed.Application.Organizations;
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

        s.AddScoped<IOrganizationRepository, OrganizationRepository>();
        s.AddScoped<IClock, SystemClock>();
        s.AddScoped<IEmailSender, NoOpEmailSender>();
        return s;
    }
}
```

- [ ] **Step 4: `Program.cs`** (cookie config + authz + migrate dev)

```csharp
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<SeedDbContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
```

- [ ] **Step 5: connection string dev/design-time**

`appsettings.Development.json` deve conter:
```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "ConnectionStrings": { "Default": "Host=localhost;Port=5432;Database=seed;Username=seed;Password=seed_dev_password" }
}
```
> Em Docker, `ConnectionStrings__Default` vem por variável de ambiente e
> sobrescreve este valor (já configurado no docker-compose.yml).

- [ ] **Step 6: gerar migration**

Run:
```
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project apps/api/src/Seed.Infrastructure --startup-project apps/api/src/Seed.Api -o Persistence/Migrations
```
Expected: cria arquivos em `Seed.Infrastructure/Persistence/Migrations`.

- [ ] **Step 7: build + commit**

Run: `dotnet build apps/api/Seed.slnx`
Expected: sucesso.
```bash
git add apps/api
git commit -m "feat(api): wiring de Identity/cookie, DbContext e migration InitialCreate"
```

---

## Task 5: Endpoints de autenticação

**Files:**
- Create: `apps/api/src/Seed.Api/Controllers/AuthController.cs`

- [ ] **Step 1: `AuthController`**

Comportamento:
- `POST /auth/register`: cria `ApplicationUser` (UserName = email), cria org +
  membership owner (via `IOrganizationService.CreateAsync` após autenticar o
  usuário), faz `SignInManager.SignInAsync`. Retorna `{ user, organization }`.
- `POST /auth/login`: `SignInManager.PasswordSignInAsync`. 200 `{ user }` / 401.
- `POST /auth/logout`: `SignInManager.SignOutAsync`. 204.
- `GET /auth/me` (`[Authorize]`): retorna user + memberships.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Abstractions;
using Seed.Application.Organizations;
using Seed.Infrastructure.Identity;

namespace Seed.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOrganizationService organizations,
    ICurrentUser currentUser) : ControllerBase
{
    public record RegisterRequest(string OrganizationName, string FullName, string Email, string Password);
    public record LoginRequest(string Email, string Password);

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
    {
        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, FullName = req.FullName };
        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await signInManager.SignInAsync(user, isPersistent: true);
        var org = await organizations.CreateAsync(new CreateOrganizationRequest(req.OrganizationName), ct);
        return Ok(new { user = new { user.Id, user.Email, user.FullName }, organization = org });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var result = await signInManager.PasswordSignInAsync(req.Email, req.Password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded) return Unauthorized();
        var user = await userManager.FindByEmailAsync(req.Email);
        return Ok(new { user = new { user!.Id, user.Email, user.FullName } });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(currentUser.UserId!.ToString()!);
        var orgs = await organizations.ListAsync(ct);
        var memberships = orgs.Select(o => new MembershipDto(o.Id, o.Name, o.Role));
        return Ok(new { user = new { user!.Id, user.Email, user.FullName }, memberships });
    }
}
```
> Nota: `SignInAsync` antes de `CreateAsync` garante que `ICurrentUser.UserId`
> resolva o novo usuário dentro do mesmo request (o cookie ainda não voltou, mas
> o `HttpContext.User` é preenchido pelo SignInManager). Se `ICurrentUser` não
> resolver nesse fluxo, passe o `user.Id` explicitamente para um overload de
> `CreateAsync`. Validar no teste de registro.

- [ ] **Step 2: build + commit**

Run: `dotnet build apps/api/Seed.slnx`
```bash
git add apps/api/src/Seed.Api
git commit -m "feat(api): endpoints de auth (register/login/logout/me)"
```

---

## Task 6: `OrganizationsController` (CRUD)

**Files:**
- Create: `apps/api/src/Seed.Api/Controllers/OrganizationsController.cs`

- [ ] **Step 1: controller**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Organizations;

namespace Seed.Api.Controllers;

[ApiController]
[Authorize]
[Route("organizations")]
public class OrganizationsController(IOrganizationService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var org = await service.GetAsync(id, ct);
        return org is null ? NotFound() : Ok(org);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Nome obrigatório." });
        var org = await service.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = org.Id }, org);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateOrganizationRequest req, CancellationToken ct)
    {
        try
        {
            var org = await service.UpdateAsync(id, req, ct);
            return org is null ? NotFound() : Ok(org);
        }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await service.DeleteAsync(id, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (ForbiddenException) { return Forbid(); }
    }
}
```
> `Forbid()` com cookie auth retorna 403 (via evento configurado). OK.

- [ ] **Step 2: build + commit**

Run: `dotnet build apps/api/Seed.slnx`
```bash
git add apps/api/src/Seed.Api
git commit -m "feat(api): CRUD de organizações com enforcement de papel/tenant"
```

---

## Task 7: Testes de integração (Testcontainers)

**Files:**
- Modify: `tests/Seed.IntegrationTests/Seed.IntegrationTests.csproj` — add `Testcontainers.PostgreSql`
- Create: `tests/Seed.IntegrationTests/ApiFactory.cs`
- Create: `tests/Seed.IntegrationTests/AuthTests.cs`
- Create: `tests/Seed.IntegrationTests/OrganizationsTests.cs`
- Modify: `tests/Seed.IntegrationTests/HealthEndpointTests.cs` — usar `ApiFactory`

- [ ] **Step 1: pacote**

Run: `dotnet add tests/Seed.IntegrationTests package Testcontainers.PostgreSql`

- [ ] **Step 2: `ApiFactory`** (sobe Postgres real e injeta a connection string)

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Seed.IntegrationTests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // aplica migrations no startup
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _db.GetConnectionString()
            });
        });
    }

    public async Task InitializeAsync() => await _db.StartAsync();
    public new async Task DisposeAsync() => await _db.DisposeAsync();
}
```

- [ ] **Step 3: `AuthTests`**

```csharp
using System.Net;
using System.Net.Http.Json;

namespace Seed.IntegrationTests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_then_me_returns_user_and_org()
    {
        var client = factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/auth/register", new
        {
            organizationName = "Acme", fullName = "Ana", email = "ana@acme.com", password = "Senha123!"
        });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadAsStringAsync();
        Assert.Contains("Acme", body);
    }

    [Fact]
    public async Task Me_without_login_is_401()
    {
        var client = factory.CreateClient();
        var me = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
```

- [ ] **Step 4: `OrganizationsTests`** (CRUD + cross-tenant)

```csharp
using System.Net;
using System.Net.Http.Json;

namespace Seed.IntegrationTests;

public class OrganizationsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> RegisterClient(string email, string org)
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register", new
        {
            organizationName = org, fullName = "User", email, password = "Senha123!"
        });
        return client;
    }

    [Fact]
    public async Task Owner_can_create_and_list()
    {
        var client = await RegisterClient("owner1@x.com", "Org1");
        var create = await client.PostAsJsonAsync("/organizations", new { name = "Nova" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await client.GetFromJsonAsync<List<Dictionary<string, object>>>("/organizations");
        Assert.True(list!.Count >= 2); // Org1 (registro) + Nova
    }

    [Fact]
    public async Task Cross_tenant_get_returns_404()
    {
        var a = await RegisterClient("a@x.com", "OrgA");
        var created = await a.PostAsJsonAsync("/organizations", new { name = "SoDoA" });
        var id = (await created.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["id"].ToString();

        var b = await RegisterClient("b@x.com", "OrgB");
        var resp = await b.GetAsync($"/organizations/{id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
```

- [ ] **Step 5: ajustar `HealthEndpointTests`** para usar `ApiFactory` (mesma fixture com DB).

- [ ] **Step 6: rodar testes**

Run: `dotnet test apps/api/Seed.slnx`
Expected: todos passam (Docker precisa estar rodando para o Testcontainers).
> Se `Register` falhar por `ICurrentUser` não resolver o novo usuário no mesmo
> request, ajustar `CreateAsync` para receber `userId` explícito e passar
> `user.Id` no `AuthController.Register`.

- [ ] **Step 7: commit**
```bash
git add apps/api/tests
git commit -m "test(api): integração de auth e CRUD com Testcontainers (inclui cross-tenant)"
```

---

## Task 8: Frontend — cliente de API e autenticação

**Files:**
- Create: `apps/web/src/lib/api.ts`, `src/lib/auth.ts`
- Create: `apps/web/src/app/login/page.tsx`, `src/app/register/page.tsx`
- Create: `apps/web/src/middleware.ts`
- Modify: `apps/web/next.config.ts` — rewrite `/api` → API em dev
- Modify: `apps/web/src/app/page.tsx` — redireciona para `/companies` ou `/login`

- [ ] **Step 1: rewrite dev no `next.config.ts`**

```ts
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    // Em dev (fora do Docker), encaminha /api para a API local.
    // No Docker, o Caddy já faz o same-origin, mas o rewrite é inócuo.
    return [{ source: "/api/:path*", destination: "http://localhost:8080/:path*" }];
  },
};
export default nextConfig;
```
> **Atenção:** no container, `localhost:8080` não é a API. Para o e2e via Docker,
> confie no Caddy (same-origin) e garanta que o rewrite só valha em dev: envolver
> em `process.env.NODE_ENV !== "production" ? [...] : []`.

Versão correta:
```ts
  async rewrites() {
    if (process.env.NODE_ENV === "production") return [];
    return [{ source: "/api/:path*", destination: "http://localhost:8080/:path*" }];
  },
```

- [ ] **Step 2: cliente `api.ts`**

```ts
export type ApiError = { status: number; message: string };

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`/api${path}`, {
    method,
    credentials: "include",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    let message = res.statusText;
    try { const j = await res.json(); message = j.error ?? j.errors?.[0] ?? message; } catch {}
    throw { status: res.status, message } as ApiError;
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

export const api = {
  get: <T>(p: string) => request<T>("GET", p),
  post: <T>(p: string, b?: unknown) => request<T>("POST", p, b),
  put: <T>(p: string, b?: unknown) => request<T>("PUT", p, b),
  del: <T>(p: string) => request<T>("DELETE", p),
};
```

- [ ] **Step 3: páginas de login e registro** (client components, Tailwind).
Cada uma: formulário controlado, chama `api.post("/auth/login"|"/auth/register", ...)`,
em sucesso faz `router.push("/companies")`, em erro mostra a mensagem.

- [ ] **Step 4: `middleware.ts`** protege `/companies*` checando o cookie de sessão
(`.AspNetCore.Identity.Application`); se ausente, redireciona para `/login`.

```ts
import { NextResponse, type NextRequest } from "next/server";

export function middleware(req: NextRequest) {
  const hasSession = req.cookies.has(".AspNetCore.Identity.Application");
  if (!hasSession) return NextResponse.redirect(new URL("/login", req.url));
  return NextResponse.next();
}
export const config = { matcher: ["/companies/:path*"] };
```

- [ ] **Step 5: build + commit**

Run: `npm --prefix apps/web run build`
```bash
git add apps/web
git commit -m "feat(web): cliente de API, login/registro e proteção de rotas"
```

---

## Task 9: Frontend — CRUD de empresa

**Files:**
- Create: `apps/web/src/app/companies/page.tsx` (lista + excluir)
- Create: `apps/web/src/app/companies/new/page.tsx` (criar)
- Create: `apps/web/src/app/companies/[id]/page.tsx` (editar)
- Create: `apps/web/src/components/CompanyForm.tsx`

- [ ] **Step 1: lista** (`/companies`): `api.get<Organization[]>("/organizations")`,
mostra nome + papel, botões Editar/Excluir e link "Nova empresa"; logout no topo.
- [ ] **Step 2: criar** (`/companies/new`): `CompanyForm` → `api.post("/organizations", {name})`.
- [ ] **Step 3: editar** (`/companies/[id]`): carrega `api.get("/organizations/"+id)`,
salva com `api.put`, botão excluir com `api.del`.
- [ ] **Step 4: tipos**

```ts
export type Organization = {
  id: string; name: string; status: string; role: string;
  createdAt: string; updatedAt: string;
};
```

- [ ] **Step 5: build + commit**

Run: `npm --prefix apps/web run build`
Expected: build ok.
```bash
git add apps/web
git commit -m "feat(web): CRUD de empresa (lista, criar, editar, excluir)"
```

---

## Task 10: Verificação ponta a ponta (Docker) e ajustes

- [ ] **Step 1: subir a stack**

Run: `docker compose up -d --build`
Aguardar todos `healthy`.

- [ ] **Step 2: fluxo via HTTP (same-origin pelo Caddy)**

Registrar, logar e criar empresa via `Invoke-WebRequest` com sessão de cookie
(`-SessionVariable`/`-WebSession`) contra `http://localhost/api/...`:
1. `POST /api/auth/register` → 200 e cookie setado.
2. `GET /api/auth/me` (mesma sessão) → 200 com a org.
3. `POST /api/organizations {name}` → 201.
4. `GET /api/organizations` → contém a nova empresa.
5. `GET /` e `GET /login` → 200 (frontend serve).

- [ ] **Step 3: corrigir o que falhar** (usar depuração sistemática; um problema
por vez; re-verificar).

- [ ] **Step 4: baixar a stack**

Run: `docker compose down`

- [ ] **Step 5: commit final + push da branch**
```bash
git add -A
git commit -m "chore: verificação e ajustes do fluxo ponta a ponta"
git push -u origin feat/organizations-login-empresa
```

- [ ] **Step 6:** atualizar `docs/setup/local-environment.md` (estado do módulo)
e `docs/modules/organizations.md` (marcar critérios de aceite atendidos).

---

## Critérios de aceite do módulo

- [ ] Cadastro cria organização + usuário owner e autentica.
- [ ] Login por email+senha define cookie httpOnly; logout limpa a sessão.
- [ ] `GET /auth/me` sem sessão → 401.
- [ ] Usuário só vê/edita organizações das quais é membro.
- [ ] `owner` cria/edita/exclui; `admin` edita; acesso cross-tenant → 404.
- [ ] Exclusão é soft delete (não some do banco, some das listagens).
- [ ] `dotnet test` verde (unit + integração, incluindo cross-tenant).
- [ ] Frontend: login, registro e CRUD de empresa funcionam pelo Docker (same-origin).
- [ ] `main` e `backup/scaffold-2026-07-18` intactas; trabalho na branch feat.

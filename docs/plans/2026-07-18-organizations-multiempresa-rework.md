# Rework Multiempresa do Módulo `organizations` — Plano de Implementação

> **Para executores agênticos:** execute tarefa a tarefa, em ordem. Execução
> AUTÔNOMA: não pare para pedir decisões; siga o design aprovado em
> `docs/specs/2026-07-18-organizations-multiempresa-design.md`. Se ficar
> genuinamente bloqueado, reporte BLOCKED com o detalhe exato.

**Goal:** Substituir o modelo atual (onde `Organization` era a própria empresa)
pelo modelo multiempresa: `Organization` (tenant) → `Company` (empresa, várias
por org) → acesso explícito por usuário (`UserCompanyAccess`). Login por
seed/provisionamento (sem auto-cadastro), CRUD de empresa restrito ao acesso, e
frontend com shadcn/ui.

**Architecture:** Monólito modular .NET 10 (Api/Application/Domain/Infrastructure),
PostgreSQL via EF Core + Npgsql, Identity (cookie). Frontend Next.js 16 + Tailwind
+ shadcn/ui, same-origin via Caddy. Testes de integração com Testcontainers.

**Tech Stack:** C#/.NET 10, ASP.NET Core Identity, EF Core 10 + Npgsql, xUnit +
Testcontainers.PostgreSql, Next.js 16 + shadcn/ui.

---

## Contexto para o executor

Branch: `feat/organizations-login-empresa`. O módulo já foi implementado numa
versão ANTERIOR (Organization = empresa, com register self-service, multi-org
CRUD, OrganizationMembership). Esta rework SUBSTITUI aquele modelo. Leia os
arquivos atuais antes de alterar; muitos serão modificados ou removidos.

Ambiente (PowerShell): antes de comandos .NET/npm, rode
`$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")`.
.NET SDK 10, solution `apps/api/Seed.slnx`. `dotnet-ef` já instalado. Docker
disponível (Testcontainers e e2e). Bancos de dev/teste são efêmeros — pode
regenerar a migration `InitialCreate` do zero (não há dados de produção).

Credenciais de seed (Development): organização "Demo"; admin
`admin@demo.local` / senha `Admin123!`; empresa "Empresa Demo".

---

## Task 1: ADRs e docs do modelo multiempresa

**Files:**
- Create: `docs/decisions/ADR-0010-multi-company-model.md`
- Create: `docs/decisions/ADR-0011-ui-design-system-approach.md`
- Modify: `docs/decisions/README.md` (índice)
- Modify: `docs/decisions/ADR-0005-data-storage-and-ownership.md` (nota de refino)
- Modify: `docs/modules/organizations.md` (refletir o modelo multiempresa)

- [ ] **Step 1: ADR-0010** (Aceita) — Modelo multiempresa. Decisão: `Organization`
  é a raiz de tenancy (mantém ADR-0005); adiciona `Company` (várias por org) e
  `UserCompanyAccess` (acesso explícito por usuário); usuário pertence a uma org
  com papel `Admin`/`Member`; provisionamento por seed no MVP (super-admin
  futuro). Consequências, alternativas (org=empresa rejeitado; acesso implícito
  "admin vê tudo" rejeitado), validação. Referencie ADR-0005 e ADR-0006.

- [ ] **Step 2: ADR-0011** (Aceita) — Abordagem de UI/design system. Decisão:
  usar shadcn/ui + Tailwind (ADR-0002) como base de componentes interina;
  design system formal (tokens/tema) adiado, com ADR próprio quando tratado.

- [ ] **Step 3:** adicionar ADR-0010 e ADR-0011 (Aceitas) ao índice
  `docs/decisions/README.md`; adicionar nota em ADR-0005 apontando que ADR-0010
  refina o modelo com `Company`/`UserCompanyAccess` (sem alterar a decisão de
  `Organization` como raiz).

- [ ] **Step 4:** atualizar `docs/modules/organizations.md` para o modelo
  multiempresa (entidades Organization/Company/User/UserCompanyAccess, acesso
  explícito, provisionamento por seed, CRUD de Company). Referencie o design spec
  e ADR-0010/0011.

- [ ] **Step 5: commit**
```bash
git add docs
git commit -m "docs(adr): ADR-0010 multiempresa e ADR-0011 design system; atualiza modulo"
```

---

## Task 2: Domínio — Company, UserCompanyAccess, OrgRole; remover membership

**Files:**
- Create: `apps/api/src/Seed.Domain/Companies/Company.cs`, `CompanyStatus.cs`
- Create: `apps/api/src/Seed.Domain/Access/UserCompanyAccess.cs`
- Modify: `apps/api/src/Seed.Domain/Organizations/Organization.cs` (remover nav Memberships; add nav Companies)
- Modify: `apps/api/src/Seed.Domain/Organizations/OrganizationRole.cs` → `{ Admin = 0, Member = 1 }`
- Remove: `apps/api/src/Seed.Domain/Organizations/OrganizationMembership.cs`, `apps/api/src/Seed.Domain/Memberships/MembershipStatus.cs`

- [ ] **Step 1: `Company` + `CompanyStatus`**

```csharp
using Seed.Domain.Common;
namespace Seed.Domain.Companies;

public class Company : Entity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CompanyStatus Status { get; set; } = CompanyStatus.Active;
}
```
```csharp
namespace Seed.Domain.Companies;
public enum CompanyStatus { Active = 0, Suspended = 1 }
```

- [ ] **Step 2: `UserCompanyAccess`**

```csharp
using Seed.Domain.Common;
namespace Seed.Domain.Access;

public class UserCompanyAccess : Entity
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid OrganizationId { get; set; }
}
```

- [ ] **Step 3:** `OrganizationRole` vira `{ Admin = 0, Member = 1 }`. `Organization`
  perde a coleção `Memberships`; opcionalmente ganha `ICollection<Company> Companies`.
  Remover `OrganizationMembership.cs` e `Memberships/MembershipStatus.cs`.

- [ ] **Step 4: build + commit**

Run: `dotnet build apps/api/src/Seed.Domain/Seed.Domain.csproj`
```bash
git add apps/api/src/Seed.Domain
git commit -m "feat(domain): Company e UserCompanyAccess; remove membership"
```

---

## Task 3: Infrastructure — ApplicationUser, DbContext, migration, repositório

**Files:**
- Modify: `apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs` (add OrganizationId, OrgRole)
- Modify: `apps/api/src/Seed.Infrastructure/Persistence/SeedDbContext.cs`
- Create: `apps/api/src/Seed.Infrastructure/Persistence/CompanyRepository.cs`
- Remove: `apps/api/src/Seed.Infrastructure/Persistence/OrganizationRepository.cs`
- Remove + regenerate: `apps/api/src/Seed.Infrastructure/Persistence/Migrations/*`

- [ ] **Step 1: `ApplicationUser`**

```csharp
using Microsoft.AspNetCore.Identity;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public OrganizationRole OrgRole { get; set; } = OrganizationRole.Member;
}
```

- [ ] **Step 2: `SeedDbContext`** — DbSets `Organizations`, `Companies`,
  `UserCompanyAccesses`, `AuditEvents` (remover `Memberships`). Config:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.Entity<Organization>(e =>
    {
        e.Property(o => o.Name).IsRequired().HasMaxLength(200);
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
}
```

- [ ] **Step 3: `CompanyRepository`** implementa `ICompanyRepository` (definido na
  Application — Task 4). Enforcement de acesso nas consultas:

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Application.Companies;
using Seed.Domain.Companies;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Persistence;

public class CompanyRepository(SeedDbContext db) : ICompanyRepository
{
    public async Task<List<Company>> ListForUserAsync(Guid userId, CancellationToken ct) =>
        await (from a in db.UserCompanyAccesses
               join c in db.Companies on a.CompanyId equals c.Id
               where a.UserId == userId
               orderby c.Name
               select c).ToListAsync(ct);

    public Task<Company?> GetForUserAsync(Guid companyId, Guid userId, CancellationToken ct) =>
        (from c in db.Companies
         where c.Id == companyId
            && db.UserCompanyAccesses.Any(a => a.CompanyId == companyId && a.UserId == userId)
         select c).FirstOrDefaultAsync(ct);

    public async Task<UserContext?> GetUserContextAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        return u is null ? null : new UserContext(u.OrganizationId, u.OrgRole);
    }

    public async Task AddAsync(Company company, Seed.Domain.Access.UserCompanyAccess access, CancellationToken ct)
    {
        await db.Companies.AddAsync(company, ct);
        await db.UserCompanyAccesses.AddAsync(access, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```
> `db.Users` é o DbSet de `ApplicationUser` do `IdentityDbContext`. `UserContext`
> é definido na Application (Task 4). Import correto conforme os namespaces.

- [ ] **Step 4:** remover `OrganizationRepository.cs`.

- [ ] **Step 5: regenerar migration** (modelo mudou; sem dados de produção):
```
Remove-Item -Recurse -Force apps/api/src/Seed.Infrastructure/Persistence/Migrations
dotnet ef migrations add InitialCreate --project apps/api/src/Seed.Infrastructure --startup-project apps/api/src/Seed.Api -o Persistence/Migrations
```
> A Application/Api ainda não compilam nesta task (dependem da Task 4/5). Se o
> `dotnet ef` exigir build, faça as Tasks 4 e 5 antes de gerar a migration e mova
> este passo para o fim da Task 5. (Recomendado: gerar a migration no fim da
> Task 5, quando tudo compila.)

- [ ] **Step 6: commit** (após compilar — pode ser junto da Task 5)
```bash
git add apps/api/src/Seed.Infrastructure
git commit -m "feat(infra): ApplicationUser com org, DbContext e repositorio de Company"
```

---

## Task 4: Application — CompanyService e contratos

**Files:**
- Create: `apps/api/src/Seed.Application/Companies/Dtos.cs`, `ICompanyRepository.cs`, `ICompanyService.cs`, `CompanyService.cs`, `UserContext.cs`
- Remove: `apps/api/src/Seed.Application/Organizations/*` (OrganizationService, IOrganizationService, IOrganizationRepository, Dtos)
- Modify: `apps/api/src/Seed.Application/DependencyInjection.cs` (registrar CompanyService)

- [ ] **Step 1: `UserContext` + DTOs**

```csharp
using Seed.Domain.Organizations;
namespace Seed.Application.Companies;
public record UserContext(Guid OrganizationId, OrganizationRole OrgRole);
```
```csharp
namespace Seed.Application.Companies;
public record CreateCompanyRequest(string Name);
public record UpdateCompanyRequest(string Name);
public record CompanyDto(Guid Id, string Name, string Status, DateTime CreatedAt, DateTime UpdatedAt);
```

- [ ] **Step 2: `ICompanyRepository`**

```csharp
using Seed.Domain.Access;
using Seed.Domain.Companies;
namespace Seed.Application.Companies;

public interface ICompanyRepository
{
    Task<List<Company>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<Company?> GetForUserAsync(Guid companyId, Guid userId, CancellationToken ct);
    Task<UserContext?> GetUserContextAsync(Guid userId, CancellationToken ct);
    Task AddAsync(Company company, UserCompanyAccess access, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

- [ ] **Step 3: `ICompanyService` + `CompanyService`** (regras de acesso)

```csharp
using Seed.Application.Abstractions;
using Seed.Domain.Access;
using Seed.Domain.Companies;
using Seed.Domain.Organizations;

namespace Seed.Application.Companies;

public class ForbiddenException(string message) : Exception(message);

public interface ICompanyService
{
    Task<List<CompanyDto>> ListAsync(CancellationToken ct);
    Task<CompanyDto?> GetAsync(Guid id, CancellationToken ct);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest req, CancellationToken ct);
    Task<CompanyDto?> UpdateAsync(Guid id, UpdateCompanyRequest req, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

public class CompanyService(
    ICompanyRepository repo,
    ICurrentUser currentUser,
    IClock clock) : ICompanyService
{
    private Guid UserId => currentUser.UserId ?? throw new ForbiddenException("Não autenticado.");

    public async Task<List<CompanyDto>> ListAsync(CancellationToken ct) =>
        (await repo.ListForUserAsync(UserId, ct)).Select(Map).ToList();

    public async Task<CompanyDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await repo.GetForUserAsync(id, UserId, ct);
        return c is null ? null : Map(c);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest req, CancellationToken ct)
    {
        var ctx = await repo.GetUserContextAsync(UserId, ct)
            ?? throw new ForbiddenException("Usuário sem organização.");
        if (ctx.OrgRole != OrganizationRole.Admin)
            throw new ForbiddenException("Apenas administradores criam empresas.");

        var now = clock.UtcNow;
        var company = new Company { OrganizationId = ctx.OrganizationId, Name = req.Name.Trim(), CreatedAt = now, UpdatedAt = now };
        var access = new UserCompanyAccess { UserId = UserId, CompanyId = company.Id, OrganizationId = ctx.OrganizationId, CreatedAt = now, UpdatedAt = now };
        await repo.AddAsync(company, access, ct);
        await repo.SaveChangesAsync(ct);
        return Map(company);
    }

    public async Task<CompanyDto?> UpdateAsync(Guid id, UpdateCompanyRequest req, CancellationToken ct)
    {
        var ctx = await repo.GetUserContextAsync(UserId, ct);
        if (ctx is null || ctx.OrgRole != OrganizationRole.Admin)
            throw new ForbiddenException("Apenas administradores editam empresas.");
        var c = await repo.GetForUserAsync(id, UserId, ct);
        if (c is null) return null;
        c.Name = req.Name.Trim();
        c.UpdatedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var ctx = await repo.GetUserContextAsync(UserId, ct);
        if (ctx is null || ctx.OrgRole != OrganizationRole.Admin)
            throw new ForbiddenException("Apenas administradores excluem empresas.");
        var c = await repo.GetForUserAsync(id, UserId, ct);
        if (c is null) return false;
        c.DeletedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return true;
    }

    private static CompanyDto Map(Company c) =>
        new(c.Id, c.Name, c.Status.ToString(), c.CreatedAt, c.UpdatedAt);
}
```

- [ ] **Step 4:** remover `Organizations/*` da Application; `DependencyInjection.cs`
  registra `ICompanyService → CompanyService`.

- [ ] **Step 5: build** (após Task 5 wiring)
```bash
git add apps/api/src/Seed.Application
git commit -m "feat(app): CompanyService com acesso explicito por empresa"
```

---

## Task 5: Api — CompaniesController, AuthController (sem register), seed, DI

**Files:**
- Create: `apps/api/src/Seed.Api/Controllers/CompaniesController.cs`
- Modify: `apps/api/src/Seed.Api/Controllers/AuthController.cs` (remover register; me com org+empresas)
- Remove: `apps/api/src/Seed.Api/Controllers/OrganizationsController.cs`
- Create: `apps/api/src/Seed.Infrastructure/Persistence/DataSeeder.cs`
- Modify: `apps/api/src/Seed.Infrastructure/DependencyInjection.cs` (registrar CompanyRepository)
- Modify: `apps/api/src/Seed.Api/Program.cs` (chamar seed em Development, após Migrate)

- [ ] **Step 1: `CompaniesController`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Seed.Application.Companies;

namespace Seed.Api.Controllers;

[ApiController]
[Authorize]
[Route("companies")]
public class CompaniesController(ICompanyService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await service.GetAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCompanyRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Nome obrigatório." });
        try { var c = await service.CreateAsync(req, ct); return CreatedAtAction(nameof(Get), new { id = c.Id }, c); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCompanyRequest req, CancellationToken ct)
    {
        try { var c = await service.UpdateAsync(id, req, ct); return c is null ? NotFound() : Ok(c); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { var ok = await service.DeleteAsync(id, ct); return ok ? NoContent() : NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }
}
```

- [ ] **Step 2: `AuthController`** — remover `register`. `login`/`logout` iguais.
  `me` retorna usuário + organização + empresas acessíveis:

```csharp
[Authorize]
[HttpGet("me")]
public async Task<IActionResult> Me(CancellationToken ct)
{
    var user = await userManager.FindByIdAsync(currentUser.UserId!.ToString()!);
    var companies = await companyService.ListAsync(ct);
    return Ok(new
    {
        user = new { user!.Id, user.Email, user.FullName },
        organizationId = user.OrganizationId,
        orgRole = user.OrgRole.ToString(),
        companies
    });
}
```
> Injete `ICompanyService companyService` no `AuthController`. Remova
> `IOrganizationService` e o endpoint `register`. `login` usa
> `SignInManager.PasswordSignInAsync`.

- [ ] **Step 3: `DataSeeder`** (idempotente; cria org Demo + admin + empresa +
  acesso). Usa `UserManager<ApplicationUser>` e `SeedDbContext`.

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
            OrganizationId = org.Id, OrgRole = OrganizationRole.Admin
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
```
> `using Microsoft.Extensions.DependencyInjection;` para `GetRequiredService`.

- [ ] **Step 4: `Program.cs`** — em Development, após `Database.Migrate()`, chamar
  `await DataSeeder.SeedAsync(scope.ServiceProvider);`. `DependencyInjection` da
  Infra registra `ICompanyRepository → CompanyRepository` (remove o de organização).

- [ ] **Step 5: gerar migration** (agora que tudo compila) — ver Task 3 Step 5.

- [ ] **Step 6: build + commit**
```bash
dotnet build apps/api/Seed.slnx   # 0 erros / 0 avisos
git add apps/api
git commit -m "feat(api): CompaniesController, me com empresas, seed Demo e migration"
```

---

## Task 6: Testes de integração (multiempresa)

**Files:**
- Modify: `apps/api/tests/Seed.IntegrationTests/ApiFactory.cs` (garantir seed rodando; expor helpers para criar org/user/empresa extra)
- Replace: `AuthTests.cs`, `OrganizationsTests.cs` → `CompaniesTests.cs`
- Manter: `HealthEndpointTests.cs`

- [ ] **Step 1:** `ApiFactory` roda em Development (seed cria admin Demo). Adicionar
  helper para logar como o admin semeado e helpers que, via `IServiceScope`
  (`UserManager` + `SeedDbContext`), criem uma SEGUNDA organização + usuário +
  empresa (para testes cross-tenant) e um usuário `Member` na org Demo.

- [ ] **Step 2: `AuthTests`**
  - `Login_seeded_admin_ok`: POST `/auth/login` com `admin@demo.local`/`Admin123!` → 200.
  - `Me_without_login_401`: GET `/auth/me` sem sessão → 401.
  - `Me_returns_org_and_companies`: após login, GET `/auth/me` → contém "Empresa Demo".

- [ ] **Step 3: `CompaniesTests`**
  - `Admin_lists_only_accessible`: login admin → GET `/companies` contém "Empresa Demo".
  - `Admin_creates_company_and_sees_it`: POST `/companies {name}` → 201; aparece no GET `/companies`.
  - `Member_cannot_create`: criar usuário Member na org Demo (via helper), login, POST `/companies` → 403.
  - `No_access_get_returns_404`: criar empresa numa SEGUNDA org (via helper); admin Demo faz GET `/companies/{idDaOutraOrg}` → 404.
  - `Cross_tenant_isolation`: usuário da segunda org NÃO vê "Empresa Demo" no seu GET `/companies`.

- [ ] **Step 4: rodar**
Run: `dotnet test apps/api/Seed.slnx` (Docker rodando). Iterar até verde. Corrigir
causa-raiz no backend se algum comportamento estiver errado (depuração
sistemática, um problema por vez).

- [ ] **Step 5: commit**
```bash
git add apps/api/tests
git commit -m "test(api): integracao multiempresa (acesso explicito, cross-tenant, member)"
```

---

## Task 7: Frontend — shadcn/ui + login + CRUD de empresa (sem registro)

**Files:**
- Setup shadcn/ui em `apps/web` (componentes: button, input, label, card, table, dialog, sonner/toast conforme necessário)
- Remove: `apps/web/src/app/register/page.tsx`
- Modify: `apps/web/src/app/login/page.tsx` (shadcn)
- Modify: `apps/web/src/lib/api.ts` (mantém), `src/lib/types.ts` (Company), `src/lib/auth.ts`
- Modify: `apps/web/src/proxy.ts` (protege `/companies*`; sem `/register`)
- Modify: `apps/web/src/app/page.tsx` (login → `/companies`)
- Create: `apps/web/src/app/companies/page.tsx` (lista + excluir), `new/page.tsx`, `[id]/page.tsx`
- Create/Modify: `apps/web/src/components/CompanyForm.tsx`

- [ ] **Step 1: shadcn/ui**. Rode o init do shadcn compatível com Tailwind v4 +
  Next 16 (consulte a doc oficial/local do shadcn). Adicione os componentes
  necessários. **Fallback:** se o shadcn/ui não for compatível com o setup atual
  (Tailwind v4 / Next 16), use componentes Tailwind próprios, limpos e
  consistentes, e registre o motivo no relatório (a intenção — base de
  componentes padrão — é preservada; ADR-0011 permite o interino).

- [ ] **Step 2:** tipos e client:
```ts
export type Company = { id: string; name: string; status: string; createdAt: string; updatedAt: string };
export type Me = { user: { id: string; email: string; fullName: string }; organizationId: string; orgRole: string; companies: Company[] };
```
`api.ts` mantém `credentials: "include"` e base `/api`. `auth.ts`: `getMe()`, `logout()`.

- [ ] **Step 3:** login (shadcn) → sucesso `router.push("/companies")`. Remover
  a página de registro e qualquer link para ela.

- [ ] **Step 4:** `/companies` (lista das minhas empresas via GET `/companies`,
  com nome e status, botões Editar/Excluir, link "Nova empresa" visível só se
  `orgRole === "Admin"`, e Sair/logout). `/companies/new` (criar). `/companies/[id]`
  (editar/excluir). Usar `use(params)` para `[id]` (Next 16). Mostrar erros de API
  (403/404) de forma amigável.

- [ ] **Step 5: build**
Run: `npm --prefix apps/web run build` → 0 erros. `npm --prefix apps/web audit` → 0 vulnerabilidades.
```bash
git add apps/web
git commit -m "feat(web): shadcn/ui, login e CRUD de empresa (multiempresa)"
```

---

## Task 8: Verificação ponta a ponta (Docker) e ajustes

- [ ] **Step 1:** `docker compose up -d --build`; aguardar `healthy`. O seed roda
  em Development e cria o admin Demo.
- [ ] **Step 2:** fluxo via HTTP same-origin (`http://localhost/api/...`) com sessão
  de cookie:
  1. POST `/api/auth/login` `{ email: "admin@demo.local", password: "Admin123!" }` → 200.
  2. GET `/api/auth/me` → 200, contém "Empresa Demo" e `orgRole: "Admin"`.
  3. POST `/api/companies { name: "Filial 2" }` → 201.
  4. GET `/api/companies` → contém "Empresa Demo" e "Filial 2".
  5. GET `/login` e `/companies` (frontend) → 200.
- [ ] **Step 3:** corrigir o que falhar (um problema por vez; re-verificar).
- [ ] **Step 4:** `docker compose down`.
- [ ] **Step 5:** atualizar `docs/setup/local-environment.md` (credenciais de seed
  e como experimentar) e marcar critérios de aceite do design/módulo.
```bash
git add -A && git commit -m "chore: verificacao e2e do modelo multiempresa"
git push origin feat/organizations-login-empresa
```

---

## Critérios de aceite (do design)

- [ ] Seed cria organização Demo + admin + empresa concedida ao admin.
- [ ] Login por email+senha (cookie httpOnly); `/auth/me` sem sessão = 401.
- [ ] Usuário só vê/edita empresas concedidas, dentro da sua organização.
- [ ] Acesso a empresa de outra org ou sem concessão = 404.
- [ ] Admin cria empresa e passa a vê-la; Member não cria (403).
- [ ] Exclusão é soft delete.
- [ ] Testes de integração verdes (acesso explícito, cross-tenant, member).
- [ ] Frontend (shadcn/ui ou Tailwind padrão): login e CRUD via Docker (same-origin).
- [ ] ADR-0010 e ADR-0011 registradas; ADR-0005 referenciada.
- [ ] `main` e `backup/scaffold-2026-07-18` intactas; trabalho na branch feat.

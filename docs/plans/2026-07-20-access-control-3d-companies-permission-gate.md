# Access Control — Gate de Empresas por Permissão (Plano 3d)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development ou superpowers:executing-plans. Steps usam checkbox (`- [ ]`).

**Goal:** Trocar o gate de autorização do `CompaniesController` do papel fixo `orgRole=Admin` para as permissões configuráveis `companies.access` (ver/listar) e `companies.manage` (criar/editar/excluir), declaradas no catálogo e reconciliadas no boot. Com isso, o acesso a empresas passa a respeitar o mesmo enforcement de permissões do resto do módulo — **fechando o risco residual do Plano 3c**: um usuário desativado passa a ser bloqueado em `/companies` imediatamente (o endpoint deixa de ser só `[Authorize]`).

**Architecture:** As permissões de empresa são declaradas numa classe estática `CompaniesPermissions` (módulo `companies`) e agregadas no `PermissionCatalog` existente; o reconciliador do boot as projeta na tabela `Permission` (sem migration). O `CompaniesController` ganha `[RequirePermission]` por método (padrão do `UsersController` do 3c). A autorização por papel sai do `CompanyService` (a decisão passa a ser do gate); o serviço mantém apenas a resolução de tenancy e o eixo de empresa (`UserCompanyAccess`).

**Tech Stack:** C# / .NET 10, ASP.NET Core, EF Core + PostgreSQL, xUnit + Testcontainers.

**Depende de:** Planos 1, 2, 3a, 3b e 3c (nesta branch).

**Spec:** `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` (seções "Enforcement", "Catálogo", "Migração") · **ADR:** ADR-0012.

**Escopo (o que NÃO entra):** remoção da coluna `orgRole` e o refactor de bootstrap/seeder/`/auth/me`/testes que ela exige — **Plano 3e** (fase 2 da migração). Frontend — fora do v1. UI de conceder/revogar `UserCompanyAccess` — módulo `organizations`, fora do escopo. **Sem migration nesta fatia** (as permissões são projeção reconciliada no boot).

**IMPORTANTE — ambiente nesta máquina (Smart App Control):**
- Build (host): `dotnet build apps/api/Seed.slnx`.
- Testes: **ferramenta PowerShell** (não Bash — `pwsh` ausente no Git Bash), caminho absoluto:
  `& 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\test.ps1' [--filter ...]` (roda em container Docker).

**Por que não há migration:** a tabela `Permission` é uma projeção do catálogo do código, reconciliada no boot (`PermissionCatalogReconciler`). Adicionar `companies.*` ao catálogo faz o reconciliador inseri-las como `active` no próximo boot; o `AccessControlBootstrapper` (top-up) concede-as ao perfil "Administrador"; o owner já tem bypass. Nenhuma mudança de schema.

**Consequência de migração assumida (já registrada na spec):** usuários sem perfil (ex.: migrados de `orgRole=Member`) perdem a visão funcional de empresas até receberem um perfil que conceda `companies.access` — mesmo com `UserCompanyAccess`. É esperado ("nenhuma permissão padrão para membro").

---

## File Structure

**Criar:**
- `apps/api/src/Seed.Application/Companies/CompaniesPermissions.cs` — declaração das permissões.
- `apps/api/tests/Seed.IntegrationTests/CompaniesEnforcementTests.cs` — testes do novo gate + bloqueio de inativo.

**Modificar:**
- `apps/api/src/Seed.Application/AccessControl/PermissionCatalog.cs` — agregar `CompaniesPermissions.Definitions`.
- `apps/api/src/Seed.Api/Controllers/CompaniesController.cs` — `[RequirePermission]` por método.
- `apps/api/src/Seed.Application/Companies/CompanyService.cs` — remover as checagens de `orgRole`.
- `apps/api/tests/Seed.IntegrationTests/ApiFactory.cs` — `CreateSecondTenantAsync` marca o usuário como `IsOwner` (a org criada em runtime não passa pelo bootstrap do boot).

---

## Task 1: Declarar `companies.access` / `companies.manage` e agregar no catálogo

**Files:**
- Create: `apps/api/src/Seed.Application/Companies/CompaniesPermissions.cs`
- Modify: `apps/api/src/Seed.Application/AccessControl/PermissionCatalog.cs`

- [ ] **Step 1: Declaração das permissões**

`apps/api/src/Seed.Application/Companies/CompaniesPermissions.cs`:

```csharp
using Seed.Application.AccessControl;

namespace Seed.Application.Companies;

// Permissões da funcionalidade de empresas (parte do módulo organizations, ADR-0010).
// Chaves estáveis e imutáveis. companies.access = ver/acessar; companies.manage =
// criar/editar/excluir. A visibilidade continua também condicionada ao eixo de
// empresa (UserCompanyAccess) — as duas travas são avaliadas juntas.
public static class CompaniesPermissions
{
    public const string Module = "companies";

    public const string Access = "companies.access";
    public const string Manage = "companies.manage";

    public static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        new(Access, Module, "Acessar empresas",
            "Ver e acessar a funcionalidade de empresas."),
        new(Manage, Module, "Gerir empresas",
            "Criar, editar e excluir empresas."),
    ];
}
```

- [ ] **Step 2: Agregar no `PermissionCatalog`**

`apps/api/src/Seed.Application/AccessControl/PermissionCatalog.cs` — adicione o `using` e concatene as definições:

```csharp
using Seed.Application.Companies;

namespace Seed.Application.AccessControl;

// Junta as declarações de todos os módulos. Ao adicionar um módulo novo com
// permissões, concatene suas Definitions aqui.
public class PermissionCatalog : IPermissionCatalog
{
    public IReadOnlyList<PermissionDefinition> All { get; } =
        [.. AccessControlPermissions.Definitions, .. CompaniesPermissions.Definitions];
}
```

- [ ] **Step 3: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Seed.Application/Companies/CompaniesPermissions.cs \
        apps/api/src/Seed.Application/AccessControl/PermissionCatalog.cs
git commit -m "feat(access-control): declara companies.access/manage no catalogo"
```

---

## Task 2: `CompaniesController` protegido por permissão

**Files:**
- Modify: `apps/api/src/Seed.Api/Controllers/CompaniesController.cs`

- [ ] **Step 1: Trocar `[Authorize]` de classe por `[RequirePermission]` por método**

`apps/api/src/Seed.Api/Controllers/CompaniesController.cs` (substitua o arquivo inteiro):

```csharp
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.Companies;

namespace Seed.Api.Controllers;

// Gate por permissão (substitui o antigo orgRole=Admin): companies.access para
// ver/listar; companies.manage para criar/editar/excluir. O eixo de empresa
// (UserCompanyAccess) continua aplicado no serviço — recurso fora do acesso → 404.
[ApiController]
[Route("companies")]
public class CompaniesController(ICompanyService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(CompaniesPermissions.Access)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(CompaniesPermissions.Access)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await service.GetAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    [RequirePermission(CompaniesPermissions.Manage)]
    public async Task<IActionResult> Create(CreateCompanyRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { error = "Nome obrigatório." });
        try { var c = await service.CreateAsync(req, ct); return CreatedAtAction(nameof(Get), new { id = c.Id }, c); }
        catch (ForbiddenException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CompaniesPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, UpdateCompanyRequest req, CancellationToken ct)
    {
        var c = await service.UpdateAsync(id, req, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(CompaniesPermissions.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await service.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
```

Notas: (a) `Update`/`Delete` deixam de capturar `ForbiddenException` porque o serviço não a lança mais nesses caminhos (a autorização agora é o gate); `Create` mantém o catch por causa do caso "usuário sem organização". (b) `RequirePermission : AuthorizeAttribute`, então também exige autenticação — não é preciso `[Authorize]` de classe.

- [ ] **Step 2: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Seed.Api/Controllers/CompaniesController.cs
git commit -m "feat(access-control): CompaniesController usa companies.access/manage (fecha risco residual 3c)"
```

---

## Task 3: Remover a autorização por `orgRole` do `CompanyService`

**Files:**
- Modify: `apps/api/src/Seed.Application/Companies/CompanyService.cs`

- [ ] **Step 1: Tirar as checagens de papel (mantendo tenancy e eixo de empresa)**

Em `apps/api/src/Seed.Application/Companies/CompanyService.cs`, substitua os três métodos `CreateAsync`/`UpdateAsync`/`DeleteAsync` por estas versões (o resto do arquivo — `UserId`, `ListAsync`, `GetAsync`, `Map` — fica igual):

```csharp
    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest req, CancellationToken ct)
    {
        // Ainda precisamos da organização do usuário para criar a empresa sob o
        // tenant correto. A autorização (companies.manage) já foi feita no gate.
        var ctx = await repo.GetUserContextAsync(UserId, ct)
            ?? throw new ForbiddenException("Usuário sem organização.");

        var now = clock.UtcNow;
        var company = new Company { OrganizationId = ctx.OrganizationId, Name = req.Name.Trim(), CreatedAt = now, UpdatedAt = now };
        var access = new UserCompanyAccess { UserId = UserId, CompanyId = company.Id, OrganizationId = ctx.OrganizationId, CreatedAt = now, UpdatedAt = now };
        await repo.AddAsync(company, access, ct);
        await repo.SaveChangesAsync(ct);
        return Map(company);
    }

    public async Task<CompanyDto?> UpdateAsync(Guid id, UpdateCompanyRequest req, CancellationToken ct)
    {
        // Autorização funcional no gate (companies.manage); aqui só o eixo de
        // empresa: sem acesso à empresa alvo → null → 404 (não vaza existência).
        var c = await repo.GetForUserAsync(id, UserId, ct);
        if (c is null) return null;
        c.Name = req.Name.Trim();
        c.UpdatedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return Map(c);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var c = await repo.GetForUserAsync(id, UserId, ct);
        if (c is null) return false;
        c.DeletedAt = clock.UtcNow;
        await repo.SaveChangesAsync(ct);
        return true;
    }
```

Nota: `ForbiddenException` continua existindo e é lançada apenas em `CreateAsync` (usuário sem organização). As checagens `ctx.OrgRole != OrganizationRole.Admin` saem. O `using Seed.Domain.Organizations;` pode ficar (a `UserContext` ainda referencia `OrganizationRole`); se o build acusar `using` não usado, é só warning — não remova nada além das checagens. O campo `OrgRole` só é removido no Plano 3e.

- [ ] **Step 2: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Seed.Application/Companies/CompanyService.cs
git commit -m "refactor(access-control): CompanyService deixa a autorizacao para o gate de permissao"
```

---

## Task 4: Ajustar a factory de testes (owner da segunda org)

**Files:**
- Modify: `apps/api/tests/Seed.IntegrationTests/ApiFactory.cs`

**Por quê:** `CreateSecondTenantAsync` cria uma organização + usuário **em runtime**, depois do boot. O `AccessControlBootstrapper` só roda no startup, então esse usuário não vira owner nem ganha o perfil "Administrador" — e, sob o novo gate, não teria `companies.access`, quebrando `CompaniesTests.Cross_tenant_isolation`. Como esse usuário representa o admin/owner da outra org (owner é gerido fora da app), marcamos `IsOwner = true` na criação — o bypass funcional passa a valer, refletindo a realidade.

- [ ] **Step 1: `IsOwner = true` no usuário da segunda org**

Em `apps/api/tests/Seed.IntegrationTests/ApiFactory.cs`, no método `CreateSecondTenantAsync`, adicione `IsOwner = true` ao inicializador do `ApplicationUser`:

```csharp
        var user = new ApplicationUser
        {
            UserName = userEmail,
            Email = userEmail,
            EmailConfirmed = true,
            FullName = userEmail,
            OrganizationId = org.Id,
            OrgRole = OrganizationRole.Admin,
            IsOwner = true, // owner da nova org (gerido fora da app; bootstrap só roda no boot)
        };
```

- [ ] **Step 2: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Seed.IntegrationTests/ApiFactory.cs
git commit -m "test(access-control): segunda org de teste nasce com owner (bypass funcional)"
```

---

## Task 5: Testes de enforcement do gate de empresas

**Files:**
- Create: `apps/api/tests/Seed.IntegrationTests/CompaniesEnforcementTests.cs`

- [ ] **Step 1: Escrever os testes**

`apps/api/tests/Seed.IntegrationTests/CompaniesEnforcementTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.Companies;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Enforcement do gate de empresas por permissão (companies.access / companies.manage),
// que substitui o antigo orgRole. Também prova que um usuário desativado é bloqueado
// em /companies — fechamento do risco residual do Plano 3c.
public class CompaniesEnforcementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<Guid> CreateMemberAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, OrganizationRole.Member);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }

    private async Task GiveProfileAsync(Guid userId, string profileName, params string[] keys)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var now = DateTime.UtcNow;
        var profile = new Profile
        {
            OrganizationId = orgId, Name = profileName, Status = ProfileStatus.Active,
            CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        foreach (var k in keys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = k });
        db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task No_companies_permission_is_forbidden()
    {
        // Membro sem perfil: nenhuma permissão funcional → nem lista empresas.
        await CreateMemberAsync("comp.noperm@demo.local");
        var client = await factory.CreateLoggedInClientAsync("comp.noperm@demo.local", "Passw0rd!");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/companies")).StatusCode);
    }

    [Fact]
    public async Task Access_lists_but_manage_is_required_to_create()
    {
        var userId = await CreateMemberAsync("comp.access@demo.local");
        await GiveProfileAsync(userId, "Só Acesso Empresas", CompaniesPermissions.Access);
        var client = await factory.CreateLoggedInClientAsync("comp.access@demo.local", "Passw0rd!");

        // companies.access basta para listar (mesmo sem UserCompanyAccess → lista vazia, 200).
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/companies")).StatusCode);
        // Mas criar exige companies.manage.
        var create = await client.PostAsJsonAsync("/companies", new { name = "Não pode" });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Manage_permission_can_create()
    {
        var userId = await CreateMemberAsync("comp.manage@demo.local");
        await GiveProfileAsync(userId, "Gestor de Empresas",
            CompaniesPermissions.Access, CompaniesPermissions.Manage);
        var client = await factory.CreateLoggedInClientAsync("comp.manage@demo.local", "Passw0rd!");

        var create = await client.PostAsJsonAsync("/companies", new { name = "Filial via permissão" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_is_blocked_on_companies()
    {
        // Fechamento do risco residual do 3c: com o gate por permissão, desativar
        // bloqueia /companies imediatamente (antes, /companies era só [Authorize]).
        var userId = await CreateMemberAsync("comp.deact@demo.local");
        await GiveProfileAsync(userId, "Acesso Empresas Deact", CompaniesPermissions.Access);
        var client = await factory.CreateLoggedInClientAsync("comp.deact@demo.local", "Passw0rd!");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/companies")).StatusCode);

        var owner = await factory.CreateAdminClientAsync();
        var deact = await owner.PatchAsJsonAsync($"/users/{userId}/status", new { active = false });
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);

        // Sessão ainda válida, mas permissão efetiva agora é vazia → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/companies")).StatusCode);
    }
}
```

- [ ] **Step 2: Rodar os testes de empresas (novos + os antigos, que mudaram de razão)**

Run: `& 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\test.ps1' --filter "FullyQualifiedName~Companies"`
Expected: `CompaniesEnforcementTests` (4) + `CompaniesTests` (5) verdes = 9. Em especial, confirme que `CompaniesTests.Cross_tenant_isolation`, `Member_cannot_create` e `No_access_get_returns_404` continuam verdes com o novo gate.

- [ ] **Step 3: Suíte completa (sem regressão)**

Run: `& 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\test.ps1'`
Expected: Passed! Todos verdes (46 anteriores + 4 = 50: 1 unit + 49 integração).

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Seed.IntegrationTests/CompaniesEnforcementTests.cs
git commit -m "test(access-control): gate de empresas por permissao e bloqueio de inativo"
```

---

## Self-Review

**Cobertura do escopo (3d):**
- `companies.access`/`companies.manage` declaradas e reconciliadas no boot (sem migration) — Task 1. ✅
- `CompaniesController` protegido por `[RequirePermission]` por método (access p/ ler, manage p/ mutar) — Task 2. ✅
- Autorização por `orgRole` removida do `CompanyService`; tenancy e eixo de empresa preservados — Task 3. ✅
- **Risco residual do 3c fechado:** usuário desativado bloqueado em `/companies` — Tasks 2, 5. ✅
- Factory de teste ajustada para o owner da segunda org (bypass) — Task 4. ✅
- Testes: separação access/manage, sem-permissão negado, bloqueio de inativo; regressão dos `CompaniesTests` antigos verificada — Task 5. ✅

**Sem mudança fora de escopo:** coluna `orgRole`, `ApplicationUser.OrgRole`, bootstrap, seeder e `/auth/me` **intocados** (Plano 3e). Sem migration. Sem frontend.

**Consistência:** `CompaniesPermissions` espelha `AccessControlPermissions` (constantes + `Definitions`); catálogo agrega ambos; gate por método como no `UsersController` (3c). Owner mantém acesso via bypass; Administrador ganha `companies.*` via top-up do bootstrap.

**Pontos de atenção para o revisor:** (a) `CompaniesTests` antigos passam a valer pela **permissão** e não pelo papel — o member sem perfil dá 403 por falta de `companies.manage`, não por `orgRole=Member`; (b) segunda org de teste agora nasce com owner — verificar que isso não mascara nenhuma asserção cross-tenant (o isolamento é pelo eixo de empresa/tenant, não pelo papel); (c) reconciliador projeta `companies.*` em todo boot — bases já existentes recebem as permissões no próximo restart, e o Administrador as recebe via top-up.

**Ambiente:** testes via **ferramenta PowerShell** (container). Sem `dotnet test`/`dotnet ef` no host (SAC). Esta fatia não gera migration.

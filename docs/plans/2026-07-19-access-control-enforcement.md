# Access Control — Enforcement Core (Plano 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir o núcleo de autorização do backend: resolver a permissão efetiva do usuário (união dos perfis ativos + bypass do owner) e aplicá-la via `[RequirePermission("key")]`, com o primeiro endpoint protegido (`GET /permissions`) para validar o enforcement de ponta a ponta.

**Architecture:** Camadas Domain/Application/Infrastructure/Api. A resolução de permissão efetiva é um serviço scoped que lê o banco por request (sem cache entre requests → revogação imediata). O enforcement usa o pipeline idiomático do ASP.NET Core: `IAuthorizationPolicyProvider` cria policies `perm:<key>` sob demanda; um `AuthorizationHandler` consulta o serviço de permissão efetiva.

**Tech Stack:** C# / .NET 10, ASP.NET Core Authorization, EF Core + PostgreSQL, xUnit + Testcontainers.

**Depende de:** Plano 1 (fundação — entidades, catálogo, reconciliador) já implementado nesta branch.

**Spec:** `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` · **ADR:** ADR-0012.

**Escopo deste plano (o que NÃO entra):** endpoints de perfis/usuários, atribuição, `/auth/me`, emissão de auditoria, seed do "Administrador", troca do gate de `companies` para `companies.manage`, remoção de `orgRole`, frontend — planos seguintes. Aqui a única mudança de comportamento é o **novo** endpoint `GET /permissions`; nada existente é alterado.

**Ambiente:** Docker ativo (Testcontainers). Comandos a partir de `apps/api`.

---

## File Structure

**Criar:**
- `src/Seed.Application/AccessControl/IEffectivePermissions.cs` — contrato da resolução de permissão efetiva.
- `src/Seed.Application/AccessControl/IPermissionQuery.cs` — leitura do catálogo ativo.
- `src/Seed.Application/AccessControl/PermissionDtos.cs` — DTOs de saída do catálogo.
- `src/Seed.Infrastructure/AccessControl/EffectivePermissionsService.cs` — impl (lê o banco).
- `src/Seed.Infrastructure/AccessControl/PermissionQuery.cs` — impl (lê o banco).
- `src/Seed.Api/Authorization/PermissionRequirement.cs`
- `src/Seed.Api/Authorization/RequirePermissionAttribute.cs`
- `src/Seed.Api/Authorization/PermissionPolicyProvider.cs`
- `src/Seed.Api/Authorization/PermissionAuthorizationHandler.cs`
- `src/Seed.Api/Controllers/PermissionsController.cs`
- `tests/Seed.IntegrationTests/AccessControlEnforcementTests.cs`

**Modificar:**
- `src/Seed.Infrastructure/DependencyInjection.cs` — registrar os dois serviços scoped.
- `src/Seed.Api/Program.cs` — registrar policy provider + handler.

---

## Task 1: Resolução de permissão efetiva

**Files:**
- Create: `src/Seed.Application/AccessControl/IEffectivePermissions.cs`
- Create: `src/Seed.Infrastructure/AccessControl/EffectivePermissionsService.cs`
- Modify: `src/Seed.Infrastructure/DependencyInjection.cs`
- Test: `tests/Seed.IntegrationTests/AccessControlEnforcementTests.cs` (parte 1)

- [ ] **Step 1: Definir o contrato**

`src/Seed.Application/AccessControl/IEffectivePermissions.cs`:

```csharp
namespace Seed.Application.AccessControl;

// Resolve o conjunto de permissões efetivas do usuário atual: união das
// permissões ativas dos perfis ativos vinculados, com bypass total para o owner.
// Recalculado por request (sem cache entre requests → revogação imediata).
public interface IEffectivePermissions
{
    Task<IReadOnlySet<string>> ForCurrentUserAsync(CancellationToken ct);
    Task<bool> HasAsync(string permissionKey, CancellationToken ct);
}
```

- [ ] **Step 2: Implementar o serviço (lendo o banco)**

`src/Seed.Infrastructure/AccessControl/EffectivePermissionsService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

public class EffectivePermissionsService(SeedDbContext db, ICurrentUser currentUser)
    : IEffectivePermissions
{
    public async Task<IReadOnlySet<string>> ForCurrentUserAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (userId is null) return new HashSet<string>();

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);
        if (user is null) return new HashSet<string>();

        // Owner: bypass funcional total (todas as permissões ativas do catálogo).
        if (user.IsOwner)
        {
            var all = await db.Permissions
                .Where(p => p.Status == PermissionStatus.Active)
                .Select(p => p.Key)
                .ToListAsync(ct);
            return all.ToHashSet();
        }

        // União das permissões ativas dos perfis ATIVOS (o query filter de
        // soft-delete de Profile já exclui perfis deletados; o Status exclui
        // arquivados). Permissões obsoletas são ignoradas.
        var keys = await (
            from up in db.UserProfiles
            join pr in db.Profiles on up.ProfileId equals pr.Id
            join pp in db.ProfilePermissions on pr.Id equals pp.ProfileId
            join perm in db.Permissions on pp.PermissionKey equals perm.Key
            where up.UserId == userId.Value
                  && pr.Status == ProfileStatus.Active
                  && perm.Status == PermissionStatus.Active
            select perm.Key
        ).Distinct().ToListAsync(ct);

        return keys.ToHashSet();
    }

    public async Task<bool> HasAsync(string permissionKey, CancellationToken ct)
        => (await ForCurrentUserAsync(ct)).Contains(permissionKey);
}
```

- [ ] **Step 3: Registrar no DI**

Em `src/Seed.Infrastructure/DependencyInjection.cs`, adicionar antes do `return s;`:

```csharp
        s.AddScoped<IEffectivePermissions, AccessControl.EffectivePermissionsService>();
```

Adicionar o `using Seed.Application.AccessControl;` no topo (junto aos demais `using`).

- [ ] **Step 4: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 5: Escrever o teste do serviço (união + owner + arquivado excluído)**

Criar `tests/Seed.IntegrationTests/AccessControlEnforcementTests.cs`:

```csharp
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Identity;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Enforcement: resolução de permissão efetiva e proteção do endpoint /permissions.
public class AccessControlEnforcementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Cria um perfil ativo na org com as permissões dadas e vincula ao usuário.
    private static async Task GiveProfileAsync(
        SeedDbContext db, Guid orgId, Guid userId, string profileName,
        ProfileStatus status, params string[] permissionKeys)
    {
        var now = DateTime.UtcNow;
        var profile = new Profile
        {
            OrganizationId = orgId, Name = profileName, Status = status,
            CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        foreach (var key in permissionKeys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = key });
        db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid userId, Guid orgId)> CreateUserAsync(
        ApiFactory factory, string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, Seed.Domain.Organizations.OrganizationRole.Member);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var id = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
        return (id, orgId);
    }

    [Fact]
    public async Task Effective_permissions_are_union_and_exclude_archived()
    {
        var (userId, orgId) = await CreateUserAsync(factory, "union@demo.local");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        await GiveProfileAsync(db, orgId, userId, "Ativo A", ProfileStatus.Active,
            AccessControlPermissions.ProfilesManage);
        await GiveProfileAsync(db, orgId, userId, "Ativo B", ProfileStatus.Active,
            AccessControlPermissions.UsersManage);
        await GiveProfileAsync(db, orgId, userId, "Arquivado", ProfileStatus.Archived,
            AccessControlPermissions.ProfilesAssign);

        // Resolve as permissões efetivas simulando o usuário logado.
        var resolver = new Seed.Infrastructure.AccessControl.EffectivePermissionsService(
            db, new FixedCurrentUser(userId));
        var perms = await resolver.ForCurrentUserAsync(default);

        Assert.Contains(AccessControlPermissions.ProfilesManage, perms);
        Assert.Contains(AccessControlPermissions.UsersManage, perms);
        Assert.DoesNotContain(AccessControlPermissions.ProfilesAssign, perms); // perfil arquivado
    }

    [Fact]
    public async Task Owner_gets_all_active_permissions()
    {
        var (userId, _) = await CreateUserAsync(factory, "owner@demo.local");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.IsOwner = true;
        await db.SaveChangesAsync();

        var resolver = new Seed.Infrastructure.AccessControl.EffectivePermissionsService(
            db, new FixedCurrentUser(userId));
        var perms = await resolver.ForCurrentUserAsync(default);

        // Owner recebe todas as permissões ativas do catálogo, sem perfil algum.
        Assert.Contains(AccessControlPermissions.ProfilesManage, perms);
        Assert.Contains(AccessControlPermissions.ProfilesAssign, perms);
        Assert.Contains(AccessControlPermissions.UsersManage, perms);
    }

    // ICurrentUser fixo para testar o serviço fora do pipeline HTTP.
    private sealed class FixedCurrentUser(Guid id) : Seed.Application.Abstractions.ICurrentUser
    {
        public Guid? UserId => id;
        public bool IsAuthenticated => true;
    }
}
```

- [ ] **Step 6: Rodar os testes do serviço**

Run: `dotnet test Seed.slnx --filter "FullyQualifiedName~AccessControlEnforcementTests"`
Expected: os 2 testes deste passo passam
(`Effective_permissions_are_union_and_exclude_archived`, `Owner_gets_all_active_permissions`).

- [ ] **Step 7: Commit**

```bash
git add src/Seed.Application/AccessControl/IEffectivePermissions.cs src/Seed.Infrastructure/AccessControl/EffectivePermissionsService.cs src/Seed.Infrastructure/DependencyInjection.cs tests/Seed.IntegrationTests/AccessControlEnforcementTests.cs
git commit -m "feat(access-control): resolucao de permissao efetiva (uniao + owner bypass)"
```

---

## Task 2: Infra de enforcement `RequirePermission`

**Files:**
- Create: `src/Seed.Api/Authorization/PermissionRequirement.cs`
- Create: `src/Seed.Api/Authorization/RequirePermissionAttribute.cs`
- Create: `src/Seed.Api/Authorization/PermissionPolicyProvider.cs`
- Create: `src/Seed.Api/Authorization/PermissionAuthorizationHandler.cs`
- Modify: `src/Seed.Api/Program.cs`

- [ ] **Step 1: Requirement**

`src/Seed.Api/Authorization/PermissionRequirement.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

public class PermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
```

- [ ] **Step 2: Atributo**

`src/Seed.Api/Authorization/RequirePermissionAttribute.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace Seed.Api.Authorization;

// [RequirePermission("profiles.manage")] — exige a permissão funcional no
// backend. O enforcement de empresa (UserCompanyAccess) é aplicado à parte.
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";
    public RequirePermissionAttribute(string permissionKey) => Policy = PolicyPrefix + permissionKey;
}
```

- [ ] **Step 3: Policy provider (cria policies `perm:<key>` sob demanda)**

`src/Seed.Api/Authorization/PermissionPolicyProvider.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Seed.Api.Authorization;

// Cria policies "perm:<key>" dinamicamente; delega o resto ao provider padrão
// (mantém o [Authorize] simples funcionando).
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var key = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(key))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}
```

- [ ] **Step 4: Handler (consulta a permissão efetiva)**

`src/Seed.Api/Authorization/PermissionAuthorizationHandler.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Seed.Application.AccessControl;

namespace Seed.Api.Authorization;

public class PermissionAuthorizationHandler(IEffectivePermissions permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await permissions.HasAsync(requirement.PermissionKey, CancellationToken.None))
            context.Succeed(requirement);
    }
}
```

- [ ] **Step 5: Registrar no `Program.cs`**

Em `src/Seed.Api/Program.cs`, logo após a linha `builder.Services.AddAuthorization();`, adicionar:

```csharp
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider,
    Seed.Api.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    Seed.Api.Authorization.PermissionAuthorizationHandler>();
```

(O handler é scoped porque depende de `IEffectivePermissions`, que é scoped.)

- [ ] **Step 6: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 7: Commit**

```bash
git add src/Seed.Api/Authorization src/Seed.Api/Program.cs
git commit -m "feat(access-control): enforcement RequirePermission (policy provider + handler)"
```

---

## Task 3: Endpoint `GET /permissions`

**Files:**
- Create: `src/Seed.Application/AccessControl/PermissionDtos.cs`
- Create: `src/Seed.Application/AccessControl/IPermissionQuery.cs`
- Create: `src/Seed.Infrastructure/AccessControl/PermissionQuery.cs`
- Create: `src/Seed.Api/Controllers/PermissionsController.cs`
- Modify: `src/Seed.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: DTOs de saída**

`src/Seed.Application/AccessControl/PermissionDtos.cs`:

```csharp
namespace Seed.Application.AccessControl;

public record PermissionItemDto(string Key, string DisplayName, string Description);
public record PermissionGroupDto(string Module, IReadOnlyList<PermissionItemDto> Permissions);
```

- [ ] **Step 2: Contrato da query**

`src/Seed.Application/AccessControl/IPermissionQuery.cs`:

```csharp
namespace Seed.Application.AccessControl;

// Lê o catálogo de permissões ATIVAS (a projeção reconciliada), agrupado por
// módulo, para alimentar a tela de edição de perfil.
public interface IPermissionQuery
{
    Task<IReadOnlyList<PermissionGroupDto>> ListActiveGroupedAsync(CancellationToken ct);
}
```

- [ ] **Step 3: Implementação (lê o banco)**

`src/Seed.Infrastructure/AccessControl/PermissionQuery.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

public class PermissionQuery(SeedDbContext db) : IPermissionQuery
{
    public async Task<IReadOnlyList<PermissionGroupDto>> ListActiveGroupedAsync(CancellationToken ct)
    {
        var perms = await db.Permissions
            .Where(p => p.Status == PermissionStatus.Active)
            .OrderBy(p => p.Module).ThenBy(p => p.DisplayName)
            .ToListAsync(ct);

        return perms
            .GroupBy(p => p.Module)
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.Select(p => new PermissionItemDto(p.Key, p.DisplayName, p.Description)).ToList()))
            .ToList();
    }
}
```

- [ ] **Step 4: Registrar no DI**

Em `src/Seed.Infrastructure/DependencyInjection.cs`, adicionar antes do `return s;`:

```csharp
        s.AddScoped<IPermissionQuery, AccessControl.PermissionQuery>();
```

- [ ] **Step 5: Controller protegido**

`src/Seed.Api/Controllers/PermissionsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.AccessControl;

namespace Seed.Api.Controllers;

[ApiController]
[Route("permissions")]
public class PermissionsController(IPermissionQuery query) : ControllerBase
{
    // Requer profiles.manage: só quem monta perfis precisa ver o catálogo.
    [HttpGet]
    [RequirePermission(AccessControlPermissions.ProfilesManage)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await query.ListActiveGroupedAsync(ct));
}
```

- [ ] **Step 6: Build**

Run: `dotnet build Seed.slnx`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 7: Commit**

```bash
git add src/Seed.Application/AccessControl/PermissionDtos.cs src/Seed.Application/AccessControl/IPermissionQuery.cs src/Seed.Infrastructure/AccessControl/PermissionQuery.cs src/Seed.Api/Controllers/PermissionsController.cs src/Seed.Infrastructure/DependencyInjection.cs
git commit -m "feat(access-control): endpoint GET /permissions (catalogo ativo agrupado)"
```

---

## Task 4: Testes de enforcement do endpoint (401/403/200 + owner)

**Files:**
- Modify: `tests/Seed.IntegrationTests/AccessControlEnforcementTests.cs`

- [ ] **Step 1: Adicionar os testes do endpoint**

Adicionar estes métodos dentro da classe `AccessControlEnforcementTests` (antes do
`private sealed class FixedCurrentUser`):

```csharp
    [Fact]
    public async Task Get_permissions_requires_authentication()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Get_permissions_forbidden_without_permission()
    {
        await CreateUserAsync(factory, "noperm@demo.local");
        var client = await factory.CreateLoggedInClientAsync("noperm@demo.local", "Passw0rd!");

        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Get_permissions_ok_with_profiles_manage()
    {
        var (userId, orgId) = await CreateUserAsync(factory, "canlist@demo.local");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            await GiveProfileAsync(db, orgId, userId, "Gestor de Perfis", ProfileStatus.Active,
                AccessControlPermissions.ProfilesManage);
        }

        var client = await factory.CreateLoggedInClientAsync("canlist@demo.local", "Passw0rd!");
        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var groups = await resp.Content.ReadFromJsonAsync<List<PermissionGroupDto>>();
        Assert.NotNull(groups);
        Assert.Contains(groups!, g => g.Permissions.Any(p => p.Key == AccessControlPermissions.ProfilesManage));
    }

    [Fact]
    public async Task Get_permissions_ok_for_owner_without_profile()
    {
        var (userId, _) = await CreateUserAsync(factory, "ownerlist@demo.local");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var user = await db.Users.FirstAsync(u => u.Id == userId);
            user.IsOwner = true;
            await db.SaveChangesAsync();
        }

        var client = await factory.CreateLoggedInClientAsync("ownerlist@demo.local", "Passw0rd!");
        var resp = await client.GetAsync("/permissions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
```

Adicionar os `using` que faltarem no topo do arquivo:
`using System.Net.Http.Json;`

- [ ] **Step 2: Rodar os testes de enforcement**

Run: `dotnet test Seed.slnx --filter "FullyQualifiedName~AccessControlEnforcementTests"`
Expected: os 6 testes passam (2 do serviço + 4 do endpoint).

- [ ] **Step 3: Rodar a suíte inteira (sem regressão)**

Run: `dotnet test Seed.slnx`
Expected: Passed! Todos os testes verdes — inclusive `CompaniesTests`/`AuthTests`
(que continuam usando `[Authorize]` simples, não afetados pelo policy provider).

- [ ] **Step 4: Commit**

```bash
git add tests/Seed.IntegrationTests/AccessControlEnforcementTests.cs
git commit -m "test(access-control): enforcement do endpoint /permissions (401/403/200/owner)"
```

---

## Self-Review

**Cobertura do escopo (enforcement core):**
- Resolução de permissão efetiva (união dos perfis ativos + owner bypass, obsoletas/arquivadas excluídas) — Task 1. ✅
- Enforcement `RequirePermission` via policy provider + handler — Task 2. ✅
- Endpoint protegido `GET /permissions` (catálogo ativo agrupado) — Task 3. ✅
- Testes: união/owner do serviço; 401/403/200/owner do endpoint — Tasks 1 e 4. ✅
- Reviewer #2 do Plano 1 (filtrar perfil ativo na resolução) — endereçado pelo `Status == Active` + query filter. ✅

**Não muda comportamento existente:** `[Authorize]` simples segue via fallback do
policy provider; `companies`/`orgRole` intocados. Única adição visível: `GET
/permissions`.

**Placeholders:** nenhum — todo passo traz código/comando concreto.

**Consistência de tipos:** `IEffectivePermissions.HasAsync/ForCurrentUserAsync`
usados igual no handler e nos testes. `RequirePermissionAttribute.PolicyPrefix`
(`"perm:"`) idêntico no atributo e no provider. `PermissionGroupDto`/
`PermissionItemDto` idênticos na query, no controller e no teste. As permissões
vêm de `AccessControlPermissions.*` (Plano 1).

**Observação:** rodar na branch `feat/access-control` (worktree), com Docker ativo.

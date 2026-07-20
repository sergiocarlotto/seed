# Access Control — Usuários + Atribuição de Perfis + `/auth/me` (Plano 3c)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development ou superpowers:executing-plans. Steps usam checkbox (`- [ ]`).

**Goal:** Gestão de usuários da organização (listar, ver, ativar/desativar — `users.manage`), atribuição do conjunto de perfis de um usuário (`profiles.assign`) e extensão de `GET /auth/me` com as permissões efetivas. É a fatia **mais sensível em segurança**: modela a **desativação** de usuário (bloqueio imediato de acesso), impõe **postura B anti-escalada** (perfil `is_system` só atribuível pelo owner), mantém o **owner somente-leitura** na aplicação e emite **auditoria old/new** das mutações.

**Architecture:** `UserService` na camada Infrastructure com `DbContext` direto (consistente com `ProfileService`/`EffectivePermissionsService`). Controller fino, enforcement por método via `[RequirePermission]` (dois gates distintos no mesmo controller: `users.manage` para gestão, `profiles.assign` para atribuição). Auditoria via `IAuditLog` (mesma UoW). Desativação modelada como `UserStatus` em `ApplicationUser`, **refletida na resolução da permissão efetiva** (usuário `Inactive` → conjunto vazio, bloqueio imediato) e no login.

**Tech Stack:** C# / .NET 10, ASP.NET Core, EF Core + PostgreSQL, System.Text.Json, xUnit + Testcontainers.

**Depende de:** Planos 1, 2, 3a e 3b (nesta branch).

**Spec:** `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` · **ADR:** ADR-0012.

**Escopo (o que NÃO entra):** troca do gate de `companies` (orgRole → `companies.access`/`companies.manage`) e remoção da coluna `orgRole` — **Plano 3d**. Frontend (telas de Perfis/Usuários), convite por email, UI de concessão de empresa e visualizador de auditoria — fora do v1. Concessão/revogação de `UserCompanyAccess` continua no módulo `organizations` (aqui as empresas são **apenas exibidas**).

**IMPORTANTE — ambiente nesta máquina (Smart App Control):**
- Testes: **ferramenta PowerShell** (não o Bash), na raiz do repo: `./scripts/test.ps1 [--filter ...]`. `pwsh` **não** existe no Git Bash; rodar via Bash falha com `pwsh: command not found`.
- Migrations: **ferramenta PowerShell**, `./scripts/ef.ps1 migrations add <Nome> -o Persistence/Migrations` (gera em container; requer Docker; a propriedade nova precisa existir e o build passar **antes** de gerar).
- `dotnet build apps/api/Seed.slnx` roda no host (checagem de compilação).

**Allow-list (anti mass-assignment):** os DTOs de request expõem **apenas** o mínimo: `UpdateUserStatusRequest(bool Active)` e `AssignProfilesRequest(Guid[] ProfileIds)`. `IsOwner`, `Status`, `OrganizationId` e `IsSystem` **não existem** nos DTOs → o cliente não consegue enviá-los. É o mecanismo de allow-list, igual ao Plano 3b.

---

## Invariantes de segurança desta fatia (checklist de aceitação)

1. **Desativação bloqueia imediatamente:** usuário `Inactive` tem permissão efetiva **vazia** no próximo request (sem cache entre requests) e **não consegue logar** de novo.
2. **Owner é somente-leitura na app:** não pode ser desativado/ativado nem ter perfis editados pela aplicação (independe de quem chama) → `400`.
3. **Postura B anti-escalada:** um chamador **não-owner** (mesmo com `profiles.assign`) **não** pode adicionar nem remover o perfil `is_system` ("Administrador") de ninguém → `403`. Só o owner mexe em `is_system`.
4. **Isolamento por tenant:** `user id` fora da org → `404`; `profile_id` fora da org em `PUT /users/{id}/profiles` → `404` (não vaza existência).
5. **`is_owner` nunca via API:** nenhum endpoint seta `IsOwner` (allow-list estrutural).
6. **Auditoria old/new** de `status_changed`, `profile_assigned`, `profile_removed` na mesma transação.

---

## File Structure

**Criar:**
- `apps/api/src/Seed.Domain/Organizations/UserStatus.cs` — enum `Active`/`Inactive` (co-locado com `OrganizationRole`, que também é um enum do usuário na org).
- `apps/api/src/Seed.Application/AccessControl/UserDtos.cs` — DTOs de usuários/atribuição.
- `apps/api/src/Seed.Application/AccessControl/IUserService.cs` — contrato + exceções.
- `apps/api/src/Seed.Infrastructure/AccessControl/UserService.cs` — impl (DbContext direto).
- `apps/api/src/Seed.Api/Controllers/UsersController.cs`
- Migration `..._AddUserStatus.cs` (+ `.Designer.cs` + snapshot) — gerada por `ef.ps1`.
- `apps/api/tests/Seed.IntegrationTests/UsersTests.cs`

**Modificar:**
- `apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs` — add `Status`.
- `apps/api/src/Seed.Infrastructure/AccessControl/EffectivePermissionsService.cs` — gate de `Inactive`.
- `apps/api/src/Seed.Api/Controllers/AuthController.cs` — `/auth/me` com `permissions`; bloqueio de login de inativo.
- `apps/api/src/Seed.Infrastructure/DependencyInjection.cs` — registrar `IUserService`.

---

## Task 1: `UserStatus` + `Status` no `ApplicationUser` + migration

**Files:**
- Create: `apps/api/src/Seed.Domain/Organizations/UserStatus.cs`
- Modify: `apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs`
- Create (gerada): migration `..._AddUserStatus`

- [ ] **Step 1: Enum de status do usuário**

`apps/api/src/Seed.Domain/Organizations/UserStatus.cs`:

```csharp
namespace Seed.Domain.Organizations;

// Situação do usuário na organização. Inactive = desativado: acesso bloqueado
// imediatamente (permissão efetiva vazia) e login recusado. Armazenado como int
// (default 0 = Active), consistente com ProfileStatus/PermissionStatus.
public enum UserStatus
{
    Active = 0,
    Inactive = 1,
}
```

- [ ] **Step 2: Campo `Status` no `ApplicationUser`**

Em `apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs`, adicione a propriedade (o `using Seed.Domain.Organizations;` já existe por causa de `OrganizationRole`):

```csharp
    // Situação do usuário. Inactive é setado via PATCH /users/{id}/status
    // (users.manage). Refletido na resolução da permissão efetiva
    // (EffectivePermissionsService) e no login → bloqueio imediato.
    public UserStatus Status { get; set; } = UserStatus.Active;
```

- [ ] **Step 3: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors. (A propriedade precisa compilar antes de gerar a migration.)

- [ ] **Step 4: Gerar a migration (container, via ferramenta PowerShell)**

Run (ferramenta PowerShell, raiz do repo): `./scripts/ef.ps1 migrations add AddUserStatus -o Persistence/Migrations`

Verifique o `..._AddUserStatus.cs` gerado: deve conter um `AddColumn<int>` de `Status` em `AspNetUsers`, `nullable: false`, `defaultValue: 0` (usuários existentes → `Active`). Não deve haver outras mudanças de schema. Se o arquivo tiver alterações inesperadas, pare e investigue antes de commitar.

- [ ] **Step 5: Build de novo (host)** para garantir que a migration gerada compila.

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Seed.Domain/Organizations/UserStatus.cs \
        apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs \
        apps/api/src/Seed.Infrastructure/Persistence/Migrations
git commit -m "feat(access-control): UserStatus (Active/Inactive) + migration AddUserStatus"
```

---

## Task 2: Gate de usuário desativado na permissão efetiva

**Files:**
- Modify: `apps/api/src/Seed.Infrastructure/AccessControl/EffectivePermissionsService.cs`

- [ ] **Step 1: Bloquear inativo antes do bypass do owner**

Substitua o bloco que projeta apenas `IsOwner` por uma projeção que também traz `Status`, e retorne conjunto vazio quando `Inactive`. O owner nunca é desativável (Task 4), mas o gate vem **antes** do bypass por defesa em profundidade.

Em `EffectivePermissionsService.ForCurrentUserAsync`, troque:

```csharp
        var isOwner = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => (bool?)u.IsOwner)
            .FirstOrDefaultAsync(ct);
        if (isOwner is null) return _cache = new HashSet<string>();

        // Owner: bypass funcional total (todas as permissões ativas do catálogo).
        if (isOwner.Value)
```

por:

```csharp
        var info = await db.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.IsOwner, u.Status })
            .FirstOrDefaultAsync(ct);
        if (info is null) return _cache = new HashSet<string>();

        // Usuário desativado: acesso bloqueado imediatamente, independente de
        // perfil ou owner (o cache morre com o request → revogação imediata).
        if (info.Status == UserStatus.Inactive) return _cache = new HashSet<string>();

        // Owner: bypass funcional total (todas as permissões ativas do catálogo).
        if (info.IsOwner)
```

Adicione o `using Seed.Domain.Organizations;` no topo do arquivo (para `UserStatus`).

- [ ] **Step 2: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Seed.Infrastructure/AccessControl/EffectivePermissionsService.cs
git commit -m "feat(access-control): usuario desativado tem permissao efetiva vazia (bloqueio imediato)"
```

---

## Task 3: DTOs, contrato e exceções do serviço de usuários

**Files:**
- Create: `apps/api/src/Seed.Application/AccessControl/UserDtos.cs`
- Create: `apps/api/src/Seed.Application/AccessControl/IUserService.cs`

- [ ] **Step 1: DTOs**

`apps/api/src/Seed.Application/AccessControl/UserDtos.cs`:

```csharp
namespace Seed.Application.AccessControl;

// Referências enxutas para os chips da tela de usuários (perfis atribuídos e
// empresas acessíveis). Só id + nome — a gestão de cada um vive no seu módulo.
public record UserProfileRefDto(Guid Id, string Name);
public record UserCompanyRefDto(Guid Id, string Name);

// Item da listagem e detalhe do usuário (mesma forma). IsOwner marca o dono da
// organização (somente-leitura na app). Companies são apenas exibidas.
public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Status,
    bool IsOwner,
    IReadOnlyList<UserProfileRefDto> Profiles,
    IReadOnlyList<UserCompanyRefDto> Companies);

// Requests (allow-list — nada de IsOwner/Status/OrganizationId/IsSystem).
public record UpdateUserStatusRequest(bool Active);
public record AssignProfilesRequest(IReadOnlyList<Guid>? ProfileIds);
```

- [ ] **Step 2: Contrato + exceções**

`apps/api/src/Seed.Application/AccessControl/IUserService.cs`:

```csharp
namespace Seed.Application.AccessControl;

// Violação de regra de negócio na gestão de usuários (ex.: tentar gerir o owner
// pela aplicação). O controller mapeia para 400.
public class UserValidationException(string message) : Exception(message);

// Refusa por autorização insuficiente sem ser falta de permissão de rota:
// não-owner tentando mexer no perfil is_system (postura B). → 403.
public class UserForbiddenException(string message) : Exception(message);

// Recurso referenciado (usuário ou profile_id) fora da org do chamador. → 404,
// sem vazar existência (ADR-0010).
public class UserNotFoundException(string message) : Exception(message);

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);
    Task<UserDto?> GetAsync(Guid id, CancellationToken ct);
    // Ativa/desativa (soft). Recusa o owner. Retorna null se o usuário não é da org.
    Task<UserDto?> SetStatusAsync(Guid id, UpdateUserStatusRequest req, CancellationToken ct);
    // Define o CONJUNTO de perfis do usuário. Retorna null se o usuário não é da org.
    Task<UserDto?> SetProfilesAsync(Guid id, AssignProfilesRequest req, CancellationToken ct);
}
```

- [ ] **Step 3: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Seed.Application/AccessControl/UserDtos.cs \
        apps/api/src/Seed.Application/AccessControl/IUserService.cs
git commit -m "feat(access-control): DTOs e contrato do servico de usuarios"
```

---

## Task 4: `UserService` (listagem, status, atribuição, invariantes, auditoria)

**Files:**
- Create: `apps/api/src/Seed.Infrastructure/AccessControl/UserService.cs`
- Modify: `apps/api/src/Seed.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Implementar o serviço**

`apps/api/src/Seed.Infrastructure/AccessControl/UserService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Application.Audit;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Gestão de usuários da organização da sessão. Consistente com ProfileService:
// DbContext direto, tenancy resolvida pelo usuário atual, auditoria na mesma UoW.
public class UserService(
    SeedDbContext db, ICurrentUser currentUser, IAuditLog audit) : IUserService
{
    private const string EntityType = "User";

    // Contexto do chamador: organização + se é owner (define quem mexe em is_system).
    private async Task<(Guid OrgId, bool IsOwner)> CallerAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UserForbiddenException("Não autenticado.");
        var caller = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.OrganizationId, u.IsOwner })
            .FirstOrDefaultAsync(ct)
            ?? throw new UserForbiddenException("Usuário sem organização.");
        return (caller.OrganizationId, caller.IsOwner);
    }

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var (orgId, _) = await CallerAsync(ct);
        return await BuildAsync(orgId, onlyUserId: null, ct);
    }

    public async Task<UserDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var (orgId, _) = await CallerAsync(ct);
        var list = await BuildAsync(orgId, onlyUserId: id, ct);
        return list.Count == 0 ? null : list[0];
    }

    public async Task<UserDto?> SetStatusAsync(Guid id, UpdateUserStatusRequest req, CancellationToken ct)
    {
        var (orgId, _) = await CallerAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == orgId, ct);
        if (user is null) return null;

        // Owner é somente-leitura na app: piso que impede lockout (ver spec).
        if (user.IsOwner)
            throw new UserValidationException("O owner não pode ser ativado ou desativado pela aplicação.");

        var newStatus = req.Active ? UserStatus.Active : UserStatus.Inactive;
        if (user.Status != newStatus)
        {
            var old = user.Status;
            user.Status = newStatus;
            audit.Record(orgId, "access_control.user.status_changed", EntityType, id.ToString(),
                new { field = "status", old = old.ToString(), @new = newStatus.ToString() });
            await db.SaveChangesAsync(ct);
        }

        var list = await BuildAsync(orgId, onlyUserId: id, ct);
        return list[0];
    }

    public async Task<UserDto?> SetProfilesAsync(Guid id, AssignProfilesRequest req, CancellationToken ct)
    {
        var (orgId, callerIsOwner) = await CallerAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == orgId, ct);
        if (user is null) return null;

        // Owner: perfis geridos fora da aplicação.
        if (user.IsOwner)
            throw new UserValidationException("Os perfis do owner não são editáveis pela aplicação.");

        var requested = (req.ProfileIds ?? []).Distinct().ToList();

        // Todos os profile_id pedidos precisam ser da org (senão 404 — não vaza).
        var requestedInOrg = requested.Count == 0
            ? new List<Guid>()
            : await db.Profiles.Where(p => requested.Contains(p.Id) && p.OrganizationId == orgId)
                .Select(p => p.Id).ToListAsync(ct);
        if (requestedInOrg.Count != requested.Count)
            throw new UserNotFoundException("Perfil inexistente nesta organização.");

        var current = await db.UserProfiles.Where(up => up.UserId == id)
            .Select(up => up.ProfileId).ToListAsync(ct);

        var toAdd = requested.Except(current).ToList();
        var toRemove = current.Except(requested).ToList();

        // Metadados (nome + is_system) de todo perfil tocado — para o gate de
        // postura B e para o nome nos eventos de auditoria.
        var touched = toAdd.Concat(toRemove).Distinct().ToList();
        var meta = touched.Count == 0
            ? new Dictionary<Guid, (string Name, bool IsSystem)>()
            : await db.Profiles.Where(p => touched.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.IsSystem })
                .ToDictionaryAsync(p => p.Id, p => (p.Name, p.IsSystem), ct);

        // Postura B: só o owner adiciona ou remove um perfil is_system.
        if (!callerIsOwner && meta.Values.Any(m => m.IsSystem))
            throw new UserForbiddenException(
                "Apenas o owner pode atribuir ou remover o perfil de sistema.");

        foreach (var pid in toAdd)
        {
            db.UserProfiles.Add(new UserProfile { UserId = id, ProfileId = pid });
            audit.Record(orgId, "access_control.user.profile_assigned", EntityType, id.ToString(),
                new { profile_id = pid, profile_name = meta[pid].Name, old = false, @new = true });
        }
        foreach (var pid in toRemove)
        {
            var row = await db.UserProfiles.FirstAsync(up => up.UserId == id && up.ProfileId == pid, ct);
            db.UserProfiles.Remove(row);
            audit.Record(orgId, "access_control.user.profile_removed", EntityType, id.ToString(),
                new { profile_id = pid, profile_name = meta[pid].Name, old = true, @new = false });
        }

        if (toAdd.Count > 0 || toRemove.Count > 0)
            await db.SaveChangesAsync(ct);

        var list = await BuildAsync(orgId, onlyUserId: id, ct);
        return list[0];
    }

    // Monta os UserDto (usuário + chips de perfis e empresas) com poucas queries
    // agregadas (sem N+1). onlyUserId != null restringe a um único usuário.
    private async Task<List<UserDto>> BuildAsync(Guid orgId, Guid? onlyUserId, CancellationToken ct)
    {
        var usersQuery = db.Users.Where(u => u.OrganizationId == orgId);
        if (onlyUserId is not null)
            usersQuery = usersQuery.Where(u => u.Id == onlyUserId.Value);

        var users = await usersQuery
            .OrderBy(u => u.FullName).ThenBy(u => u.Email)
            .Select(u => new
            {
                u.Id, u.FullName, u.Email, u.Status, u.IsOwner,
            })
            .ToListAsync(ct);
        if (users.Count == 0) return [];

        var ids = users.Select(u => u.Id).ToList();

        var profiles = await (
            from up in db.UserProfiles
            join p in db.Profiles on up.ProfileId equals p.Id
            where p.OrganizationId == orgId && ids.Contains(up.UserId)
            select new { up.UserId, p.Id, p.Name }
        ).ToListAsync(ct);

        var companies = await (
            from a in db.UserCompanyAccesses
            join c in db.Companies on a.CompanyId equals c.Id
            where a.OrganizationId == orgId && ids.Contains(a.UserId)
            select new { a.UserId, c.Id, c.Name }
        ).ToListAsync(ct);

        var profilesByUser = profiles.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(x => x.Name)
                .Select(x => new UserProfileRefDto(x.Id, x.Name)).ToList());
        var companiesByUser = companies.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(x => x.Name)
                .Select(x => new UserCompanyRefDto(x.Id, x.Name)).ToList());

        return users.Select(u => new UserDto(
            u.Id, u.FullName, u.Email ?? "", u.Status.ToString(), u.IsOwner,
            profilesByUser.GetValueOrDefault(u.Id, []),
            companiesByUser.GetValueOrDefault(u.Id, []))).ToList();
    }
}
```

- [ ] **Step 2: Registrar no DI**

Em `apps/api/src/Seed.Infrastructure/DependencyInjection.cs`, junto dos outros `AddScoped` (perto de `IProfileService`), antes do `return s;`:

```csharp
        s.AddScoped<IUserService, AccessControl.UserService>();
```

- [ ] **Step 3: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Seed.Infrastructure/AccessControl/UserService.cs \
        apps/api/src/Seed.Infrastructure/DependencyInjection.cs
git commit -m "feat(access-control): UserService (gestao, atribuicao, postura B, auditoria)"
```

---

## Task 5: `UsersController` (dois gates: users.manage e profiles.assign)

**Files:**
- Create: `apps/api/src/Seed.Api/Controllers/UsersController.cs`

- [ ] **Step 1: Controller**

`apps/api/src/Seed.Api/Controllers/UsersController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.AccessControl;

namespace Seed.Api.Controllers;

// Gestão de usuários. Dois gates distintos por método: users.manage para
// listar/ver/ativar-desativar; profiles.assign para atribuir perfis. Por isso o
// [RequirePermission] fica no método, não na classe.
[ApiController]
[Route("users")]
public class UsersController(IUserService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var u = await service.GetAsync(id, ct);
        return u is null ? NotFound() : Ok(u);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> SetStatus(Guid id, UpdateUserStatusRequest req, CancellationToken ct)
    {
        try
        {
            var u = await service.SetStatusAsync(id, req, ct);
            return u is null ? NotFound() : Ok(u);
        }
        catch (UserValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UserForbiddenException) { return Forbid(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/profiles")]
    [RequirePermission(AccessControlPermissions.ProfilesAssign)]
    public async Task<IActionResult> SetProfiles(Guid id, AssignProfilesRequest req, CancellationToken ct)
    {
        try
        {
            var u = await service.SetProfilesAsync(id, req, ct);
            return u is null ? NotFound() : Ok(u);
        }
        catch (UserValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UserForbiddenException) { return Forbid(); }
        catch (UserNotFoundException) { return NotFound(); }
    }
}
```

- [ ] **Step 2: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Seed.Api/Controllers/UsersController.cs
git commit -m "feat(access-control): UsersController (users.manage + profiles.assign)"
```

---

## Task 6: `/auth/me` com permissões + bloqueio de login de inativo

**Files:**
- Modify: `apps/api/src/Seed.Api/Controllers/AuthController.cs`

- [ ] **Step 1: Injetar `IEffectivePermissions` e estender `/auth/me`; recusar login de inativo**

Em `AuthController`:

1. Adicione o `using Seed.Application.AccessControl;` e injete `IEffectivePermissions effective` no construtor (após `ICurrentUser currentUser`).

2. No `Login`, após obter o `user` autenticado, recuse usuário desativado (o `PasswordSignInAsync` não conhece o `Status`):

```csharp
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null || user.Status == Seed.Domain.Organizations.UserStatus.Inactive)
        {
            // Desfaz o cookie recém-emitido e responde como credencial inválida
            // (não revela que a conta existe mas está desativada).
            await signInManager.SignOutAsync();
            return Unauthorized();
        }
```

3. No `Me`, adicione `permissions` (chaves efetivas) e `isOwner`:

```csharp
        var user = await userManager.FindByIdAsync(currentUser.UserId!.ToString()!);
        var companies = await companyService.ListAsync(ct);
        var permissions = await effective.ForCurrentUserAsync(ct);
        return Ok(new
        {
            user = new { user!.Id, user.Email, user.FullName },
            organizationId = user.OrganizationId,
            orgRole = user.OrgRole.ToString(),
            isOwner = user.IsOwner,
            permissions,
            companies
        });
```

(`orgRole` permanece no payload até o Plano 3d removê-lo.)

- [ ] **Step 2: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Seed.Api/Controllers/AuthController.cs
git commit -m "feat(access-control): /auth/me expoe permissoes; login recusa usuario inativo"
```

---

## Task 7: Testes de integração

**Files:**
- Create: `apps/api/tests/Seed.IntegrationTests/UsersTests.cs`

- [ ] **Step 1: Escrever os testes**

`apps/api/tests/Seed.IntegrationTests/UsersTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Gestão de usuários (users.manage), atribuição de perfis (profiles.assign),
// desativação com bloqueio imediato, postura B anti-escalada, owner read-only e
// auditoria old/new.
public class UsersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Cria um usuário na org Demo e devolve seu id. Sem perfil (zero permissão).
    private async Task<Guid> CreateMemberAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, OrganizationRole.Member);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }

    // Vincula ao usuário um perfil ativo com as permissões dadas (nome único).
    private async Task GiveProfileAsync(Guid userId, string profileName, params string[] permissionKeys)
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
        foreach (var key in permissionKeys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = key });
        db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
        await db.SaveChangesAsync();
    }

    // Client logado como um gestor (perfil ativo concedendo as permissões dadas).
    private async Task<HttpClient> ClientWithAsync(string email, params string[] permissionKeys)
    {
        var userId = await CreateMemberAsync(email);
        await GiveProfileAsync(userId, $"Perfil {email}", permissionKeys);
        return await factory.CreateLoggedInClientAsync(email, "Passw0rd!");
    }

    private async Task<Guid> DemoAdminIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == ApiFactory.AdminEmail).Select(u => u.Id).FirstAsync();
    }

    private async Task<Guid> SystemProfileIdAsync()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Profiles.Where(p => p.OrganizationId == orgId && p.IsSystem).Select(p => p.Id).FirstAsync();
    }

    [Fact]
    public async Task List_requires_users_manage()
    {
        await CreateMemberAsync("users.noperm@demo.local");
        var client = await factory.CreateLoggedInClientAsync("users.noperm@demo.local", "Passw0rd!");
        var resp = await client.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task List_shows_members_with_profiles_and_companies()
    {
        var client = await ClientWithAsync("users.list@demo.local", AccessControlPermissions.UsersManage);
        var list = await client.GetFromJsonAsync<List<UserDto>>("/users");
        Assert.NotNull(list);

        // O admin semeado (owner) aparece marcado e com a empresa Demo acessível.
        var owner = list!.FirstOrDefault(u => u.Email == ApiFactory.AdminEmail);
        Assert.NotNull(owner);
        Assert.True(owner!.IsOwner);
        Assert.Contains(owner.Companies, c => c.Name == ApiFactory.DemoCompanyName);

        // O próprio gestor aparece com o perfil que recebeu.
        var self = list.FirstOrDefault(u => u.Email == "users.list@demo.local");
        Assert.NotNull(self);
        Assert.Contains(self!.Profiles, p => p.Name == "Perfil users.list@demo.local");
    }

    [Fact]
    public async Task Get_cross_tenant_user_is_404()
    {
        var client = await ClientWithAsync("users.get@demo.local", AccessControlPermissions.UsersManage);
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org X", companyName: "Emp X", userEmail: "x@x.local", userPassword: "Xxxx123!");
        // O usuário da outra org não é visível → 404 (não vaza existência).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var otherUserId = await db.Users.Where(u => u.Email == "x@x.local").Select(u => u.Id).FirstAsync();
        var resp = await client.GetAsync($"/users/{otherUserId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Deactivate_blocks_access_immediately()
    {
        // Gestor com users.manage é desativado pelo owner e perde acesso no
        // próximo request (sem cache entre requests).
        var targetId = await CreateMemberAsync("users.deact@demo.local");
        await GiveProfileAsync(targetId, "Perfil deact", AccessControlPermissions.UsersManage);
        var targetClient = await factory.CreateLoggedInClientAsync("users.deact@demo.local", "Passw0rd!");
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/users")).StatusCode);

        var owner = await factory.CreateAdminClientAsync();
        var deact = await owner.PatchAsJsonAsync($"/users/{targetId}/status", new { active = false });
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);
        var dto = await deact.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(UserStatus.Inactive.ToString(), dto!.Status);

        // Sessão ainda válida (cookie), mas permissão efetiva agora é vazia → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await targetClient.GetAsync("/users")).StatusCode);

        // E não consegue logar de novo.
        var relog = factory.CreateClient();
        var login = await relog.PostAsJsonAsync("/auth/login", new { email = "users.deact@demo.local", password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        // Auditoria old/new do status.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var tid = targetId.ToString();
        Assert.True(await db.AuditEvents.AnyAsync(a =>
            a.Action == "access_control.user.status_changed" && a.EntityId == tid && a.ActorUserId != null));
    }

    [Fact]
    public async Task Cannot_deactivate_owner()
    {
        var owner = await factory.CreateAdminClientAsync();
        var ownerId = await DemoAdminIdAsync();
        var resp = await owner.PatchAsJsonAsync($"/users/{ownerId}/status", new { active = false });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Assign_profiles_sets_set_and_emits_audit()
    {
        // Um gestor com profiles.assign define os perfis de um membro.
        var manager = await ClientWithAsync("users.assign@demo.local", AccessControlPermissions.ProfilesAssign);
        var targetId = await CreateMemberAsync("users.target@demo.local");

        // Cria dois perfis comuns na org para atribuir.
        Guid p1, p2;
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            var a = new Profile { OrganizationId = orgId, Name = "Comum A", Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now };
            var b = new Profile { OrganizationId = orgId, Name = "Comum B", Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now };
            db.Profiles.AddRange(a, b);
            await db.SaveChangesAsync();
            p1 = a.Id; p2 = b.Id;
        }

        var put = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { p1, p2 } });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var dto = await put.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(2, dto!.Profiles.Count);

        // Redefinir para apenas p1 remove p2 (operação de conjunto).
        var put2 = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { p1 } });
        var dto2 = await put2.Content.ReadFromJsonAsync<UserDto>();
        Assert.Single(dto2!.Profiles);
        Assert.Equal(p1, dto2.Profiles[0].Id);

        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<SeedDbContext>();
        var tid = targetId.ToString();
        Assert.True(await db2.AuditEvents.AnyAsync(a => a.Action == "access_control.user.profile_assigned" && a.EntityId == tid));
        Assert.True(await db2.AuditEvents.AnyAsync(a => a.Action == "access_control.user.profile_removed" && a.EntityId == tid));
    }

    [Fact]
    public async Task Assign_cross_tenant_profile_is_404()
    {
        var manager = await ClientWithAsync("users.xassign@demo.local", AccessControlPermissions.ProfilesAssign);
        var targetId = await CreateMemberAsync("users.xtarget@demo.local");
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org Y", companyName: "Emp Y", userEmail: "y@y.local", userPassword: "Yyyy123!");

        // Cria um perfil na outra org e tenta atribuí-lo (deve dar 404).
        Guid foreignProfile;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            var p = new Profile { OrganizationId = other.OrganizationId, Name = "Perfil Estrangeiro", Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now };
            db.Profiles.Add(p);
            await db.SaveChangesAsync();
            foreignProfile = p.Id;
        }

        var resp = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { foreignProfile } });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Non_owner_cannot_assign_system_profile_but_owner_can()
    {
        var manager = await ClientWithAsync("users.escalate@demo.local", AccessControlPermissions.ProfilesAssign);
        var targetId = await CreateMemberAsync("users.escalate.target@demo.local");
        var systemId = await SystemProfileIdAsync();

        // Postura B: não-owner com profiles.assign não atribui o "Administrador".
        var denied = await manager.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { systemId } });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // O owner consegue.
        var owner = await factory.CreateAdminClientAsync();
        var allowed = await owner.PutAsJsonAsync($"/users/{targetId}/profiles", new { profileIds = new[] { systemId } });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Cannot_edit_owner_profiles()
    {
        var owner = await factory.CreateAdminClientAsync();
        var ownerId = await DemoAdminIdAsync();
        var resp = await owner.PutAsJsonAsync($"/users/{ownerId}/profiles", new { profileIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Auth_me_exposes_permissions_and_companies()
    {
        var client = await factory.CreateAdminClientAsync();
        var me = await client.GetFromJsonAsync<MeResponse>("/auth/me");
        Assert.NotNull(me);
        Assert.True(me!.IsOwner);
        // Owner tem todas as permissões ativas do catálogo.
        Assert.Contains(AccessControlPermissions.ProfilesManage, me.Permissions);
        Assert.Contains(AccessControlPermissions.UsersManage, me.Permissions);
        Assert.Contains(me.Companies, c => c.Name == ApiFactory.DemoCompanyName);
    }

    // Espelho enxuto do payload de /auth/me para desserialização nos testes.
    private record MeResponse(
        bool IsOwner,
        List<string> Permissions,
        List<CompanyRef> Companies);
    private record CompanyRef(Guid Id, string Name);
}
```

> Nota de desserialização: `MeResponse`/`CompanyRef` capturam só o que os testes
> checam; propriedades ausentes no JSON são ignoradas. Se o `System.Text.Json`
> do projeto usar camelCase (padrão do ASP.NET Core), os nomes batem
> (`isOwner`/`permissions`/`companies`).

- [ ] **Step 2: Rodar os testes de usuários (ferramenta PowerShell)**

Run: `./scripts/test.ps1 --filter "FullyQualifiedName~UsersTests"`
Expected: 10 testes verdes.

- [ ] **Step 3: Suíte completa (sem regressão)**

Run: `./scripts/test.ps1`
Expected: Passed! Todos verdes (31 anteriores + 10 = 41).

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Seed.IntegrationTests/UsersTests.cs
git commit -m "test(access-control): usuarios, atribuicao, desativacao, postura B e /auth/me"
```

---

## Self-Review

**Cobertura do escopo (3c):**
- Endpoints de usuários (`GET /users`, `GET /users/{id}`, `PATCH /users/{id}/status`) sob `users.manage` — Tasks 4, 5. ✅
- Atribuição de perfis (`PUT /users/{id}/profiles`) sob `profiles.assign` — Tasks 4, 5. ✅
- `GET /auth/me` estendido com `permissions` (chaves efetivas) + `isOwner`; empresas já vinham — Task 6. ✅
- Desativação modelada em `ApplicationUser.Status` (migration) e refletida na permissão efetiva + login → bloqueio imediato — Tasks 1, 2, 6. ✅
- Postura B (is_system só o owner atribui/remove) — Task 4. ✅
- Owner somente-leitura (não desativável, perfis não editáveis) — Task 4. ✅
- Allow-list (DTOs sem IsOwner/Status/OrganizationId/IsSystem) — Task 3. ✅
- Tenant (user/profile de outra org → 404, sem vazar) — Task 4. ✅
- Auditoria old/new (`status_changed`, `profile_assigned`, `profile_removed`) na mesma UoW — Task 4. ✅
- Testes cobrindo enforcement, bloqueio imediato, postura B, tenant, owner e auditoria — Task 7. ✅

**Sem mudança fora de escopo:** gate de `companies` e coluna `orgRole` intocados (Plano 3d); sem frontend; sem convite por email.

**Consistência:** `UserService` espelha `ProfileService` (Infrastructure, DbContext direto, `IAuditLog` na mesma UoW, tenancy pelo usuário atual). Ações de auditoria com prefixo `access_control.user.*`. `@new` em objeto anônimo serializa como chave JSON `new`. `[RequirePermission]` no método (dois gates). `UserStatus` co-locado com `OrganizationRole` e armazenado como int (como `ProfileStatus`/`PermissionStatus`).

**Pontos de atenção para o revisor de segurança:** (a) gate de `Inactive` vem antes do bypass de owner na resolução; (b) postura B cobre adição **e** remoção de is_system por não-owner; (c) login de inativo desfaz o cookie e responde 401 genérico (não revela conta desativada); (d) profile_id de outra org → 404 (não 403), sem vazar existência.

**Ambiente:** testes e migrations via **ferramenta PowerShell** (container), não Bash (`pwsh` ausente no Git Bash) nem `dotnet test`/`dotnet ef` no host (SAC).

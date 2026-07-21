# Access Control — CRUD de Perfis + Auditoria (Plano 3b)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development ou superpowers:executing-plans. Steps usam checkbox (`- [ ]`).

**Goal:** Endpoints de CRUD de perfis (por organização, protegidos por `profiles.manage`), com invariantes de segurança (perfil de sistema imutável, nome único, permissões válidas/ativas, campos sensíveis nunca vindos do cliente) e emissão de auditoria com formato **antes/depois (`old`/`new`)** na mesma transação.

**Architecture:** `ProfileService` na camada Infrastructure com `DbContext` direto (consistente com `EffectivePermissionsService`/`PermissionQuery` do módulo). Auditoria via helper `IAuditLog` que **adiciona** `AuditEvent` ao mesmo `DbContext` (sem `SaveChanges` próprio) — o `SaveChanges` do serviço persiste mutação + auditoria atomicamente. Controller fino, enforcement via `[RequirePermission]` (Plano 2).

**Tech Stack:** C# / .NET 10, ASP.NET Core, EF Core + PostgreSQL, System.Text.Json, xUnit + Testcontainers.

**Depende de:** Planos 1, 2 e 3a (nesta branch).

**Spec:** `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` · **ADR:** ADR-0012.

**Escopo (o que NÃO entra):** endpoints de usuários, atribuição de perfis, `/auth/me`, troca do gate de `companies`, remoção de `orgRole`, frontend, visualizador de auditoria — planos seguintes. Sem migration (schema já existe).

**IMPORTANTE — testes nesta máquina:** o Smart App Control bloqueia `dotnet test` no host. Use o runner em container: PowerShell na raiz do repo, `./scripts/test.ps1 [--filter ...]`. `dotnet build Seed.slnx` no host funciona para checar compilação.

**Allow-list (anti mass-assignment):** os DTOs de request só expõem `Name`, `Description`, `PermissionKeys`. `IsSystem`, `Status`, `OrganizationId` **não existem** nos DTOs → o cliente não consegue enviá-los. Isso é intencional e é o mecanismo de allow-list.

---

## File Structure

**Criar:**
- `src/Seed.Application/Audit/IAuditLog.cs` — helper de auditoria (mesma UoW).
- `src/Seed.Infrastructure/Audit/AuditLog.cs` — impl.
- `src/Seed.Application/AccessControl/ProfileDtos.cs` — requests/responses do CRUD.
- `src/Seed.Application/AccessControl/IProfileService.cs` — contrato + exceções.
- `src/Seed.Infrastructure/AccessControl/ProfileService.cs` — impl (DbContext direto).
- `src/Seed.Api/Controllers/ProfilesController.cs`
- `tests/Seed.IntegrationTests/ProfilesTests.cs`

**Modificar:**
- `src/Seed.Infrastructure/DependencyInjection.cs` — registrar `IAuditLog` e `IProfileService` (scoped).

---

## Task 1: Helper de auditoria `IAuditLog`

**Files:**
- Create: `src/Seed.Application/Audit/IAuditLog.cs`
- Create: `src/Seed.Infrastructure/Audit/AuditLog.cs`
- Modify: `src/Seed.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Contrato**

`src/Seed.Application/Audit/IAuditLog.cs`:

```csharp
namespace Seed.Application.Audit;

// Registra um evento de auditoria na MESMA unidade de trabalho da mutação (não
// chama SaveChanges; o serviço chamador persiste tudo junto, atômico). O ator e
// o horário vêm do contexto; o chamador informa a organização e o alvo.
public interface IAuditLog
{
    void Record(Guid organizationId, string action, string entityType, string entityId, object? metadata = null);
}
```

- [ ] **Step 2: Implementação**

`src/Seed.Infrastructure/Audit/AuditLog.cs`:

```csharp
using System.Text.Json;
using Seed.Application.Abstractions;
using Seed.Application.Audit;
using Seed.Domain.Audit;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Audit;

public class AuditLog(SeedDbContext db, ICurrentUser currentUser, IClock clock) : IAuditLog
{
    public void Record(Guid organizationId, string action, string entityType, string entityId, object? metadata = null)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OccurredAt = clock.UtcNow,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
        });
    }
}
```

- [ ] **Step 3: Registrar no DI**

Em `src/Seed.Infrastructure/DependencyInjection.cs`, antes do `return s;`:

```csharp
        s.AddScoped<Seed.Application.Audit.IAuditLog, Audit.AuditLog>();
```

- [ ] **Step 4: Build**

Run: `dotnet build Seed.slnx` → Build succeeded, 0 Errors.

- [ ] **Step 5: Commit**

```bash
git add src/Seed.Application/Audit/IAuditLog.cs src/Seed.Infrastructure/Audit/AuditLog.cs src/Seed.Infrastructure/DependencyInjection.cs
git commit -m "feat(access-control): helper de auditoria IAuditLog (mesma transacao)"
```

---

## Task 2: DTOs, contrato e exceções do serviço de perfis

**Files:**
- Create: `src/Seed.Application/AccessControl/ProfileDtos.cs`
- Create: `src/Seed.Application/AccessControl/IProfileService.cs`

- [ ] **Step 1: DTOs**

`src/Seed.Application/AccessControl/ProfileDtos.cs`:

```csharp
namespace Seed.Application.AccessControl;

// Requests: só Name/Description/PermissionKeys (allow-list — IsSystem/Status/
// OrganizationId nunca vêm do cliente).
public record CreateProfileRequest(string Name, string? Description, IReadOnlyList<string>? PermissionKeys);
public record UpdateProfileRequest(string Name, string? Description, IReadOnlyList<string>? PermissionKeys);

public record ProfileSummaryDto(Guid Id, string Name, string Description, bool IsSystem, string Status, int UserCount);
public record ProfileDetailDto(Guid Id, string Name, string Description, bool IsSystem, string Status, IReadOnlyList<string> PermissionKeys);
```

- [ ] **Step 2: Contrato + exceções**

`src/Seed.Application/AccessControl/IProfileService.cs`:

```csharp
namespace Seed.Application.AccessControl;

// Violação de regra de negócio do CRUD de perfis (nome duplicado, permissão
// inválida, perfil de sistema imutável). O controller mapeia para 400.
public class ProfileValidationException(string message) : Exception(message);

// Ausência de contexto do usuário (não autenticado / sem organização). → 403.
public class ProfileForbiddenException(string message) : Exception(message);

public interface IProfileService
{
    Task<IReadOnlyList<ProfileSummaryDto>> ListAsync(CancellationToken ct);
    Task<ProfileDetailDto?> GetAsync(Guid id, CancellationToken ct);
    Task<ProfileDetailDto> CreateAsync(CreateProfileRequest req, CancellationToken ct);
    Task<ProfileDetailDto?> UpdateAsync(Guid id, UpdateProfileRequest req, CancellationToken ct);
    Task<bool> ArchiveAsync(Guid id, CancellationToken ct);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Seed.slnx` → 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add src/Seed.Application/AccessControl/ProfileDtos.cs src/Seed.Application/AccessControl/IProfileService.cs
git commit -m "feat(access-control): DTOs e contrato do servico de perfis"
```

---

## Task 3: `ProfileService` (CRUD + invariantes + auditoria)

**Files:**
- Create: `src/Seed.Infrastructure/AccessControl/ProfileService.cs`
- Modify: `src/Seed.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Implementar o serviço**

`src/Seed.Infrastructure/AccessControl/ProfileService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Application.Audit;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

public class ProfileService(
    SeedDbContext db, ICurrentUser currentUser, IClock clock, IAuditLog audit) : IProfileService
{
    private const string EntityType = "Profile";

    private async Task<Guid> OrgIdAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new ProfileForbiddenException("Não autenticado.");
        var orgId = await db.Users.Where(u => u.Id == userId)
            .Select(u => (Guid?)u.OrganizationId).FirstOrDefaultAsync(ct);
        return orgId ?? throw new ProfileForbiddenException("Usuário sem organização.");
    }

    public async Task<IReadOnlyList<ProfileSummaryDto>> ListAsync(CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var rows = await db.Profiles
            .Where(p => p.OrganizationId == orgId)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.IsSystem, p.Status,
                UserCount = db.UserProfiles.Count(up => up.ProfileId == p.Id),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ProfileSummaryDto(
                r.Id, r.Name, r.Description, r.IsSystem, r.Status.ToString(), r.UserCount))
            .ToList();
    }

    public async Task<ProfileDetailDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var p = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (p is null) return null;
        var keys = await KeysOfAsync(id, ct);
        return Map(p, keys);
    }

    public async Task<ProfileDetailDto> CreateAsync(CreateProfileRequest req, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) throw new ProfileValidationException("Nome obrigatório.");
        if (await db.Profiles.AnyAsync(p => p.OrganizationId == orgId && p.Name == name, ct))
            throw new ProfileValidationException("Já existe um perfil com esse nome.");

        var keys = await ValidateKeysAsync(req.PermissionKeys, ct);
        var desc = req.Description?.Trim() ?? "";
        var now = clock.UtcNow;

        var profile = new Profile
        {
            OrganizationId = orgId, Name = name, Description = desc,
            IsSystem = false, Status = ProfileStatus.Active, CreatedAt = now, UpdatedAt = now,
        };
        db.Profiles.Add(profile);
        foreach (var k in keys)
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = k });

        audit.Record(orgId, "access_control.profile.created", EntityType, profile.Id.ToString(),
            new { name, description = desc });
        foreach (var k in keys)
            audit.Record(orgId, "access_control.profile.permission_granted", EntityType, profile.Id.ToString(),
                new { permission_key = k, profile_name = name, old = false, @new = true });

        await db.SaveChangesAsync(ct);
        return Map(profile, keys);
    }

    public async Task<ProfileDetailDto?> UpdateAsync(Guid id, UpdateProfileRequest req, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (profile is null) return null;
        if (profile.IsSystem) throw new ProfileValidationException("Perfil de sistema não pode ser editado.");

        var name = (req.Name ?? "").Trim();
        if (name.Length == 0) throw new ProfileValidationException("Nome obrigatório.");
        if (await db.Profiles.AnyAsync(p => p.OrganizationId == orgId && p.Name == name && p.Id != id, ct))
            throw new ProfileValidationException("Já existe um perfil com esse nome.");
        var desc = req.Description?.Trim() ?? "";
        var newKeys = await ValidateKeysAsync(req.PermissionKeys, ct);

        if (name != profile.Name)
            audit.Record(orgId, "access_control.profile.updated", EntityType, id.ToString(),
                new { field = "name", old = profile.Name, @new = name });
        if (desc != profile.Description)
            audit.Record(orgId, "access_control.profile.updated", EntityType, id.ToString(),
                new { field = "description", old = profile.Description, @new = desc });
        profile.Name = name;
        profile.Description = desc;
        profile.UpdatedAt = clock.UtcNow;

        var current = await KeysOfAsync(id, ct);
        foreach (var k in newKeys.Except(current))
        {
            db.ProfilePermissions.Add(new ProfilePermission { ProfileId = id, PermissionKey = k });
            audit.Record(orgId, "access_control.profile.permission_granted", EntityType, id.ToString(),
                new { permission_key = k, profile_name = name, old = false, @new = true });
        }
        foreach (var k in current.Except(newKeys))
        {
            var row = await db.ProfilePermissions.FirstAsync(pp => pp.ProfileId == id && pp.PermissionKey == k, ct);
            db.ProfilePermissions.Remove(row);
            audit.Record(orgId, "access_control.profile.permission_revoked", EntityType, id.ToString(),
                new { permission_key = k, profile_name = name, old = true, @new = false });
        }

        await db.SaveChangesAsync(ct);
        return Map(profile, newKeys);
    }

    public async Task<bool> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var orgId = await OrgIdAsync(ct);
        var profile = await db.Profiles.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == orgId, ct);
        if (profile is null) return false;
        if (profile.IsSystem) throw new ProfileValidationException("Perfil de sistema não pode ser arquivado.");
        if (profile.Status == ProfileStatus.Archived) return true;

        profile.Status = ProfileStatus.Archived;
        profile.UpdatedAt = clock.UtcNow;
        audit.Record(orgId, "access_control.profile.archived", EntityType, id.ToString(),
            new { field = "status", old = "Active", @new = "Archived" });

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<List<string>> KeysOfAsync(Guid profileId, CancellationToken ct) =>
        await db.ProfilePermissions.Where(pp => pp.ProfileId == profileId)
            .Select(pp => pp.PermissionKey).ToListAsync(ct);

    private async Task<List<string>> ValidateKeysAsync(IReadOnlyList<string>? keys, CancellationToken ct)
    {
        var distinct = (keys ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (distinct.Count == 0) return distinct;

        var valid = await db.Permissions
            .Where(p => distinct.Contains(p.Key) && p.Status == PermissionStatus.Active)
            .Select(p => p.Key).ToListAsync(ct);

        var invalid = distinct.Except(valid).ToList();
        if (invalid.Count > 0)
            throw new ProfileValidationException(
                $"Permissões inválidas ou obsoletas: {string.Join(", ", invalid)}");
        return valid;
    }

    private static ProfileDetailDto Map(Profile p, List<string> keys) =>
        new(p.Id, p.Name, p.Description, p.IsSystem, p.Status.ToString(), keys);
}
```

- [ ] **Step 2: Registrar no DI**

Em `src/Seed.Infrastructure/DependencyInjection.cs`, antes do `return s;`:

```csharp
        s.AddScoped<IProfileService, AccessControl.ProfileService>();
```

(`using Seed.Application.AccessControl;` já deve estar presente do Plano 2; se não, adicione.)

- [ ] **Step 3: Build**

Run: `dotnet build Seed.slnx` → 0 Errors.

- [ ] **Step 4: Commit**

```bash
git add src/Seed.Infrastructure/AccessControl/ProfileService.cs src/Seed.Infrastructure/DependencyInjection.cs
git commit -m "feat(access-control): ProfileService (CRUD, invariantes, auditoria old/new)"
```

---

## Task 4: `ProfilesController`

**Files:**
- Create: `src/Seed.Api/Controllers/ProfilesController.cs`

- [ ] **Step 1: Controller**

`src/Seed.Api/Controllers/ProfilesController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Seed.Api.Authorization;
using Seed.Application.AccessControl;

namespace Seed.Api.Controllers;

[ApiController]
[Route("profiles")]
[RequirePermission(AccessControlPermissions.ProfilesManage)]
public class ProfilesController(IProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var p = await service.GetAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProfileRequest req, CancellationToken ct)
    {
        try { var p = await service.CreateAsync(req, ct); return CreatedAtAction(nameof(Get), new { id = p.Id }, p); }
        catch (ProfileValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ProfileForbiddenException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProfileRequest req, CancellationToken ct)
    {
        try { var p = await service.UpdateAsync(id, req, ct); return p is null ? NotFound() : Ok(p); }
        catch (ProfileValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ProfileForbiddenException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        try { var ok = await service.ArchiveAsync(id, ct); return ok ? NoContent() : NotFound(); }
        catch (ProfileValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (ProfileForbiddenException) { return Forbid(); }
    }
}
```

Nota: o `[RequirePermission(...)]` no nível da classe protege todos os endpoints
com `profiles.manage`.

- [ ] **Step 2: Build**

Run: `dotnet build Seed.slnx` → 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add src/Seed.Api/Controllers/ProfilesController.cs
git commit -m "feat(access-control): ProfilesController (CRUD protegido por profiles.manage)"
```

---

## Task 5: Testes de integração

**Files:**
- Create: `tests/Seed.IntegrationTests/ProfilesTests.cs`

- [ ] **Step 1: Escrever os testes**

`tests/Seed.IntegrationTests/ProfilesTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// CRUD de perfis: invariantes (sistema imutável, nome único, permissão válida) e
// auditoria (old/new) na mesma transação.
public class ProfilesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Cria um usuário com um perfil ativo concedendo profiles.manage e devolve um
    // client HTTP logado como ele.
    private async Task<HttpClient> ManagerClientAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId, OrganizationRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
            var now = DateTime.UtcNow;
            var profile = new Profile
            {
                OrganizationId = orgId, Name = $"Gestor {email}", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            db.ProfilePermissions.Add(new ProfilePermission
            {
                ProfileId = profile.Id, PermissionKey = AccessControlPermissions.ProfilesManage,
            });
            db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }
        return await factory.CreateLoggedInClientAsync(email, "Passw0rd!");
    }

    [Fact]
    public async Task Create_then_get_and_list()
    {
        var client = await ManagerClientAsync("prof.create@demo.local");

        var create = await client.PostAsJsonAsync("/profiles", new
        {
            name = "Financeiro",
            description = "Acesso financeiro",
            permissionKeys = new[] { AccessControlPermissions.UsersManage },
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProfileDetailDto>();
        Assert.NotNull(created);
        Assert.Contains(AccessControlPermissions.UsersManage, created!.PermissionKeys);
        Assert.False(created.IsSystem);

        var detail = await client.GetFromJsonAsync<ProfileDetailDto>($"/profiles/{created.Id}");
        Assert.Equal("Financeiro", detail!.Name);

        var list = await client.GetFromJsonAsync<List<ProfileSummaryDto>>("/profiles");
        Assert.Contains(list!, p => p.Name == "Financeiro");
    }

    [Fact]
    public async Task Create_duplicate_name_is_rejected()
    {
        var client = await ManagerClientAsync("prof.dup@demo.local");
        await client.PostAsJsonAsync("/profiles", new { name = "Repetido", description = (string?)null, permissionKeys = new string[0] });

        var again = await client.PostAsJsonAsync("/profiles", new { name = "Repetido", description = (string?)null, permissionKeys = new string[0] });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Create_with_invalid_permission_is_rejected()
    {
        var client = await ManagerClientAsync("prof.badperm@demo.local");

        var resp = await client.PostAsJsonAsync("/profiles", new
        {
            name = "Com permissão inválida",
            description = (string?)null,
            permissionKeys = new[] { "does.not.exist" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Update_changes_permissions_and_emits_audit()
    {
        var client = await ManagerClientAsync("prof.update@demo.local");
        var created = await (await client.PostAsJsonAsync("/profiles", new
        {
            name = "A editar",
            description = "antes",
            permissionKeys = new[] { AccessControlPermissions.UsersManage },
        })).Content.ReadFromJsonAsync<ProfileDetailDto>();

        // Troca a permissão (remove UsersManage, adiciona ProfilesAssign) e a descrição.
        var upd = await client.PutAsJsonAsync($"/profiles/{created!.Id}", new
        {
            name = "A editar",
            description = "depois",
            permissionKeys = new[] { AccessControlPermissions.ProfilesAssign },
        });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);
        var updated = await upd.Content.ReadFromJsonAsync<ProfileDetailDto>();
        Assert.Contains(AccessControlPermissions.ProfilesAssign, updated!.PermissionKeys);
        Assert.DoesNotContain(AccessControlPermissions.UsersManage, updated.PermissionKeys);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var pid = created.Id.ToString();
        var granted = await db.AuditEvents.AnyAsync(a =>
            a.Action == "access_control.profile.permission_granted" && a.EntityId == pid);
        var revoked = await db.AuditEvents.AnyAsync(a =>
            a.Action == "access_control.profile.permission_revoked" && a.EntityId == pid);
        Assert.True(granted);
        Assert.True(revoked);
        // O evento tem ator preenchido (o usuário logado).
        var hasActor = await db.AuditEvents.AnyAsync(a => a.EntityId == pid && a.ActorUserId != null);
        Assert.True(hasActor);
    }

    [Fact]
    public async Task Archive_sets_status_and_blocks_system_profile()
    {
        var client = await ManagerClientAsync("prof.archive@demo.local");
        var created = await (await client.PostAsJsonAsync("/profiles", new
        {
            name = "Para arquivar", description = (string?)null, permissionKeys = new string[0],
        })).Content.ReadFromJsonAsync<ProfileDetailDto>();

        var archive = await client.DeleteAsync($"/profiles/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        var after = await client.GetFromJsonAsync<ProfileDetailDto>($"/profiles/{created.Id}");
        Assert.Equal(ProfileStatus.Archived.ToString(), after!.Status);

        // O perfil de sistema "Administrador" não pode ser arquivado nem editado.
        var orgId = await factory.GetDemoOrganizationIdAsync();
        Guid systemId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            systemId = await db.Profiles.Where(p => p.OrganizationId == orgId && p.IsSystem).Select(p => p.Id).FirstAsync();
        }
        var blockedArchive = await client.DeleteAsync($"/profiles/{systemId}");
        Assert.Equal(HttpStatusCode.BadRequest, blockedArchive.StatusCode);
        var blockedEdit = await client.PutAsJsonAsync($"/profiles/{systemId}", new
        {
            name = "Hackeado", description = (string?)null, permissionKeys = new string[0],
        });
        Assert.Equal(HttpStatusCode.BadRequest, blockedEdit.StatusCode);
    }

    [Fact]
    public async Task Requires_profiles_manage()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync("prof.noperm@demo.local", "Passw0rd!", orgId, OrganizationRole.Member);
        var client = await factory.CreateLoggedInClientAsync("prof.noperm@demo.local", "Passw0rd!");

        var resp = await client.GetAsync("/profiles");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Rodar os testes de perfis (runner em container)**

Run (PowerShell, raiz do repo): `./scripts/test.ps1 --filter "FullyQualifiedName~ProfilesTests"`
Expected: 6 testes verdes.

- [ ] **Step 3: Suíte completa (sem regressão)**

Run (PowerShell, raiz do repo): `./scripts/test.ps1`
Expected: Passed! Todos verdes (25 anteriores + 6 = 31: 1 unit + 30 integração).

- [ ] **Step 4: Commit**

```bash
git add tests/Seed.IntegrationTests/ProfilesTests.cs
git commit -m "test(access-control): CRUD de perfis, invariantes e auditoria"
```

---

## Self-Review

**Cobertura do escopo (3b):**
- CRUD de perfis por organização protegido por `profiles.manage` — Tasks 3, 4. ✅
- Invariante nome único (checagem + índice como backstop) — Task 3. ✅
- Invariante permissão válida/ativa (rejeita inválida/obsoleta) — Task 3. ✅
- Invariante perfil de sistema imutável (não edita, não arquiva) — Task 3. ✅
- Allow-list (DTOs só com Name/Description/PermissionKeys) — Task 2. ✅
- Arquivamento via `Status` (não exclusão física) — Task 3. ✅
- Auditoria `old/new` na mesma transação (created, updated, permission_granted/revoked, archived) — Tasks 1, 3. ✅
- Testes de CRUD, invariantes, auditoria e enforcement — Task 5. ✅

**Sem mudança fora de escopo:** sem migration; usuários/atribuição/`/auth/me`/
`companies`/`orgRole` intocados.

**Placeholders:** nenhum. **Consistência:** `IProfileService`/DTOs usados igual em
service, controller e testes; ações de auditoria com prefixo
`access_control.profile.*`; `AccessControlPermissions.ProfilesManage` (Plano 1);
`[RequirePermission]` (Plano 2). `@new` em objeto anônimo serializa como chave
JSON `new`.

**Camada:** `ProfileService` em Infrastructure (DbContext direto), consistente com
`EffectivePermissionsService`/`PermissionQuery` do módulo. Auditoria via `IAuditLog`
na mesma UoW → atomicidade mutação+auditoria.

**Testes rodam via `scripts/test.ps1` (container), não `dotnet test` no host (SAC).**

# Access Control — Remoção do `orgRole` (Plano 3e, fase 2 da migração)

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development ou superpowers:executing-plans. Steps usam checkbox (`- [ ]`).

**Goal:** Concluir a migração da ADR-0012 removendo o papel fixo `orgRole` (`Admin`/`Member`), agora que toda a autorização funcional vem de perfis/permissões (Planos 2–3d) e o dono da organização é o flag técnico `is_owner`. Remove `ApplicationUser.OrgRole` e o enum `OrganizationRole`, faz o `AccessControlBootstrapper` chavear o vínculo do owner por `IsOwner` (não mais por `OrgRole==Admin`), ajusta seeder/factory/`UserContext`/`/auth/me`, e gera a **migration fase 2** que dropa a coluna `OrgRole`.

**Architecture:** Refactor mecânico, sem novo comportamento. `is_owner` já é a fonte de verdade do dono (semeado pelo `DataSeeder`/banco; gerido fora da app). O bootstrap deixa de derivar owner de `orgRole` e passa a apenas **ligar os owners existentes** ao perfil "Administrador". A autorização de empresas já é por permissão (3d), então nada lê mais `orgRole` para decidir acesso.

**Tech Stack:** C# / .NET 10, ASP.NET Core, EF Core + PostgreSQL, xUnit + Testcontainers.

**Depende de:** Planos 1, 2, 3a, 3b, 3c e 3d (nesta branch). **É o fecho da migração** — só rode depois que os dados existentes já tiverem `is_owner` populado (o bootstrap do 3a já faz isso a cada boot enquanto `orgRole` existe; nesta base só há o seed Demo, então é seguro).

**Spec:** `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` (seção "Migração", fase 2) · **ADR:** ADR-0012.

**Escopo (o que NÃO entra):** frontend (telas de Perfis/Usuários) — próxima etapa. Nenhuma mudança de comportamento de autorização (o gate de empresas já mudou no 3d).

**IMPORTANTE — ambiente nesta máquina (Smart App Control):**
- Build (host): `dotnet build apps/api/Seed.slnx`.
- Migration: `dotnet ef` NÃO roda no host. Use a ferramenta **PowerShell** (não Bash — `pwsh` ausente no Git Bash), caminho absoluto:
  `& 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\ef.ps1' migrations add DropOrgRole -o Persistence/Migrations` (gera em container Docker; requer Docker rodando; o código precisa compilar SEM `OrgRole` antes de gerar).
- Testes: ferramenta **PowerShell**, `& 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\test.ps1'` (container).

**Nota sobre usings órfãos:** o projeto NÃO trata warnings como erro (sem `TreatWarningsAsErrors`, sem `Directory.Build.props`). Remover uma referência a `OrganizationRole` pode deixar um `using Seed.Domain.Organizations;` sem uso (warning CS8019) — **não quebra o build**. Remova o using onde o arquivo não referenciar mais nada daquele namespace (tidiness); onde ainda usar `Organization`/`UserStatus`/`OrganizationStatus`, **mantenha**.

---

## File Structure

**Produção — modificar:**
- `apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs` — remove `OrgRole`.
- `apps/api/src/Seed.Infrastructure/AccessControl/AccessControlBootstrapper.cs` — chaveia por `IsOwner`.
- `apps/api/src/Seed.Infrastructure/Persistence/DataSeeder.cs` — admin nasce `IsOwner=true`.
- `apps/api/src/Seed.Infrastructure/Persistence/CompanyRepository.cs` — `UserContext` sem `OrgRole`.
- `apps/api/src/Seed.Application/Companies/UserContext.cs` — record sem `OrgRole`.
- `apps/api/src/Seed.Api/Controllers/AuthController.cs` — `/auth/me` sem `orgRole`.

**Produção — deletar:**
- `apps/api/src/Seed.Domain/Organizations/OrganizationRole.cs` — enum descontinuado.

**Testes — modificar:**
- `apps/api/tests/Seed.IntegrationTests/ApiFactory.cs` — `CreateUserAsync` sem `OrganizationRole`; segunda org sem `OrgRole`.
- `apps/api/tests/Seed.IntegrationTests/AuthTests.cs` — asserção de `orgRole` no `/auth/me` trocada.
- Call sites de `CreateUserAsync` (dropam o 4º argumento): `UsersTests.cs`, `ProfilesTests.cs` (2x), `CompaniesTests.cs`, `CompaniesEnforcementTests.cs`, `AccessControlEnforcementTests.cs`, `AccessControlBootstrapTests.cs`.

**Migration — criar (via `ef.ps1`):** `..._DropOrgRole.cs` (+ `.Designer.cs` + snapshot).

---

## Task 1: Remover `orgRole` do código (produção + testes) e deletar o enum

> Mudança atômica: `dotnet build apps/api/Seed.slnx` compila a solução inteira (inclusive testes), então TODAS as referências a `OrganizationRole` precisam sair de uma vez para o build ficar verde.

**Files:** os listados acima (produção + testes), e deletar `OrganizationRole.cs`.

- [ ] **Step 1: `ApplicationUser` — remover `OrgRole`**

`apps/api/src/Seed.Infrastructure/Identity/ApplicationUser.cs` — remova **apenas** a propriedade `OrgRole`. **Mantenha** o `using Seed.Domain.Organizations;` (ainda necessário por causa de `UserStatus`, que vive nesse namespace). O arquivo deve ficar:

```csharp
using Microsoft.AspNetCore.Identity;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }

    // Dono da organização. Semeado pelo DataSeeder/banco (gerido fora da app);
    // nenhum endpoint de API o altera. Tem bypass funcional total. O
    // AccessControlBootstrapper liga os owners ao perfil "Administrador" no boot.
    public bool IsOwner { get; set; }

    // Situação do usuário. Inactive é setado via PATCH /users/{id}/status
    // (users.manage). Refletido na resolução da permissão efetiva e no login.
    public UserStatus Status { get; set; } = UserStatus.Active;
}
```

- [ ] **Step 2: `AccessControlBootstrapper` — ligar owners por `IsOwner`**

`apps/api/src/Seed.Infrastructure/AccessControl/AccessControlBootstrapper.cs`:

Atualize o comentário de cabeçalho (linhas ~8-16) para refletir que o bootstrap **liga owners ao perfil**, sem derivar owner de `orgRole`. Substitua o parágrafo inicial por:

```csharp
// Garante, para cada organização, o perfil de sistema "Administrador" com todas
// as permissões ativas, e liga ao perfil os usuários que já são owner
// (is_owner). O owner é definido fora da aplicação (DataSeeder/banco), não por
// esta rotina. Idempotente: roda todo boot sem duplicar. Deve rodar APÓS o
// reconciliador do catálogo (precisa das permissões já projetadas na tabela).
```

E substitua o bloco `// 3. Admins da org...` (a query de `admins` e o `foreach`) por:

```csharp
            // 3. Liga os owners da org ao perfil "Administrador". is_owner é a
            // fonte de verdade do dono (semeado fora desta rotina).
            var owners = await db.Users
                .Where(u => u.OrganizationId == orgId && u.IsOwner)
                .ToListAsync(ct);
            foreach (var user in owners)
            {
                var linked = await db.UserProfiles
                    .AnyAsync(up => up.UserId == user.Id && up.ProfileId == adminProfile.Id, ct);
                if (!linked)
                    db.UserProfiles.Add(new UserProfile
                    {
                        UserId = user.Id,
                        ProfileId = adminProfile.Id,
                    });
            }
```

Remova o `using Seed.Domain.Organizations;` do topo se ficar sem uso (o arquivo não referencia mais `OrganizationRole`; confira se não usa outro tipo daquele namespace — se não, remova).

- [ ] **Step 3: `DataSeeder` — admin nasce owner**

`apps/api/src/Seed.Infrastructure/Persistence/DataSeeder.cs` — na criação do `admin`, troque `OrgRole = OrganizationRole.Admin` por `IsOwner = true`:

```csharp
        var admin = new ApplicationUser
        {
            UserName = "admin@demo.local", Email = "admin@demo.local",
            EmailConfirmed = true, FullName = "Admin Demo",
            OrganizationId = org.Id, IsOwner = true
        };
```

Mantenha o `using Seed.Domain.Organizations;` (o seeder usa `Organization`).

- [ ] **Step 4: `UserContext` — sem `OrgRole`**

`apps/api/src/Seed.Application/Companies/UserContext.cs` (arquivo inteiro):

```csharp
namespace Seed.Application.Companies;

// Contexto mínimo do usuário para o serviço de empresas: a organização (tenant)
// sob a qual ele opera. A autorização funcional é feita pelo gate de permissão.
public record UserContext(Guid OrganizationId);
```

- [ ] **Step 5: `CompanyRepository` — construir `UserContext` sem `OrgRole`**

`apps/api/src/Seed.Infrastructure/Persistence/CompanyRepository.cs`, no `GetUserContextAsync`:

```csharp
        return u is null ? null : new UserContext(u.OrganizationId);
```

Remova o `using Seed.Domain.Organizations;` se ficar sem uso.

- [ ] **Step 6: `AuthController` — tirar `orgRole` do `/auth/me`**

`apps/api/src/Seed.Api/Controllers/AuthController.cs`, no objeto de resposta do `Me`, remova a linha `orgRole = user.OrgRole.ToString(),`. O `return` fica:

```csharp
        return Ok(new
        {
            user = new { user!.Id, user.Email, user.FullName },
            organizationId = user.OrganizationId,
            isOwner = user.IsOwner,
            permissions,
            companies
        });
```

- [ ] **Step 7: Deletar o enum**

Delete o arquivo `apps/api/src/Seed.Domain/Organizations/OrganizationRole.cs`:

```bash
git rm apps/api/src/Seed.Domain/Organizations/OrganizationRole.cs
```

- [ ] **Step 8: `ApiFactory` — `CreateUserAsync` sem papel; segunda org sem `OrgRole`**

`apps/api/tests/Seed.IntegrationTests/ApiFactory.cs`:

(a) Troque a assinatura e o corpo de `CreateUserAsync` (o 4º parâmetro passa a ser `bool isOwner = false`):

```csharp
    // Cria um usuário (com senha) numa organização existente. isOwner=false por
    // padrão (usuário comum, sem perfil e sem bypass). Não concede acesso a
    // nenhuma empresa.
    public async Task CreateUserAsync(string email, string password, Guid organizationId, bool isOwner = false)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = email,
            OrganizationId = organizationId,
            IsOwner = isOwner,
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Falha ao criar usuário {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
```

(b) Em `CreateSecondTenantAsync`, remova a linha `OrgRole = OrganizationRole.Admin,` do inicializador do `ApplicationUser` (o `IsOwner = true` do Plano 3d permanece). O bloco fica:

```csharp
        var user = new ApplicationUser
        {
            UserName = userEmail,
            Email = userEmail,
            EmailConfirmed = true,
            FullName = userEmail,
            OrganizationId = org.Id,
            IsOwner = true, // owner da nova org (gerido fora da app; bootstrap só roda no boot)
        };
```

Mantenha o `using Seed.Domain.Organizations;` (a factory usa `Organization`).

- [ ] **Step 9: Dropar o 4º argumento em todos os call sites de `CreateUserAsync`**

Em cada arquivo abaixo, remova `, OrganizationRole.Member` (ou `, Seed.Domain.Organizations.OrganizationRole.Member`) da chamada, deixando `CreateUserAsync(email, senha, orgId)`:

- `apps/api/tests/Seed.IntegrationTests/UsersTests.cs` (1 chamada)
- `apps/api/tests/Seed.IntegrationTests/ProfilesTests.cs` (2 chamadas)
- `apps/api/tests/Seed.IntegrationTests/CompaniesTests.cs` (1 chamada — `member@demo.local`)
- `apps/api/tests/Seed.IntegrationTests/CompaniesEnforcementTests.cs` (1 chamada)
- `apps/api/tests/Seed.IntegrationTests/AccessControlEnforcementTests.cs` (1 chamada — remova também o prefixo `Seed.Domain.Organizations.`)
- `apps/api/tests/Seed.IntegrationTests/AccessControlBootstrapTests.cs` (1 chamada)

Onde o `using Seed.Domain.Organizations;` do arquivo ficar sem uso, pode removê-lo (tidiness) — mas cuidado: `UsersTests.cs` ainda usa `UserStatus` daquele namespace, então **mantenha** o using lá. Warnings de using órfão não quebram o build.

- [ ] **Step 10: `AuthTests` — trocar a asserção de `orgRole`**

`apps/api/tests/Seed.IntegrationTests/AuthTests.cs`, no teste `Me_returns_org_and_companies`, a linha `Assert.Contains("Admin", body); // orgRole` asserta um campo que deixa de existir. Troque por uma asserção do novo sinal de dono:

```csharp
        Assert.Contains(ApiFactory.DemoCompanyName, body); // "Empresa Demo"
        Assert.Contains("organizationId", body);
        Assert.Contains("isOwner", body); // dono da org (substitui orgRole)
        Assert.Contains("permissions", body);
```

- [ ] **Step 11: Build (host)**

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors. (Warnings de using órfão são aceitáveis.) Se houver **erro** de referência a `OrganizationRole` ou `OrgRole` que sobrou, encontre e remova essa referência (não reintroduza o enum).

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "refactor(access-control): remove orgRole do codigo (owner via is_owner); deleta OrganizationRole"
```

---

## Task 2: Migration fase 2 — dropar a coluna `OrgRole`

**Files:** migration gerada em `apps/api/src/Seed.Infrastructure/Persistence/Migrations/`.

- [ ] **Step 1: Gerar a migration (container, ferramenta PowerShell)**

Run: `& 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\ef.ps1' migrations add DropOrgRole -o Persistence/Migrations`

(Requer Docker rodando. Se falhar por daemon, inicie o Docker Desktop e repita.)

- [ ] **Step 2: Conferir a migration gerada**

Abra o `..._DropOrgRole.cs`. O `Up` deve conter **apenas**:

```csharp
migrationBuilder.DropColumn(
    name: "OrgRole",
    table: "AspNetUsers");
```

e o `Down` um `AddColumn<int>` de `OrgRole` (nullable: false, defaultValue: 0). Não deve haver nenhuma outra mudança de schema. Se houver algo inesperado, PARE e investigue (não commite).

- [ ] **Step 3: Build (host)** para garantir que a migration compila.

Run: `dotnet build apps/api/Seed.slnx` → 0 Errors.

- [ ] **Step 4: Suíte completa (via container)**

Run: `& 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\test.ps1'`
Expected: Passed! Todos verdes — mesma contagem do 3d (47: 1 unit + 46 integração). O Testcontainers aplica todas as migrations do zero (inclui `DropOrgRole`) e o `DataSeeder` cria o admin como `is_owner=true`; os testes de bootstrap/enforcement/companies/auth devem continuar verdes.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Seed.Infrastructure/Persistence/Migrations
git commit -m "feat(access-control): migration fase 2 dropa a coluna OrgRole"
```

---

## Self-Review

**Cobertura do escopo (3e):**
- `ApplicationUser.OrgRole` removido; enum `OrganizationRole` deletado — Task 1. ✅
- `AccessControlBootstrapper` liga owners por `IsOwner` (não mais `OrgRole==Admin`); não seta mais owner — Task 1. ✅
- `DataSeeder` cria o admin com `IsOwner=true` — Task 1. ✅
- `UserContext`/`CompanyRepository` sem `OrgRole` — Task 1. ✅
- `/auth/me` sem `orgRole` — Task 1. ✅
- Factory e call sites de teste ajustados; `AuthTests` asserta `isOwner`/`permissions` no lugar de `orgRole` — Task 1. ✅
- Migration fase 2 dropa a coluna `OrgRole` — Task 2. ✅

**Sem mudança de comportamento:** a autorização não muda (empresas já eram por permissão desde o 3d; owner por `is_owner` desde o 3a). Isto é limpeza + fecho da migração.

**Riscos e verificações:**
- **Owner de novas orgs:** após o 3e, quem cria uma org em runtime (onboarding futuro) precisa setar `is_owner` fora da app (mesma premissa do design). No MVP só há o seed Demo (`DataSeeder` seta) e as orgs de teste (`ApiFactory` seta). Nenhuma regressão.
- **Bootstrap idempotente:** continua ligando owners ao "Administrador"; o teste `Seeding_twice_is_idempotent` cobre. `Member_does_not_become_owner_or_linked` (usuário não-owner) segue válido: sem `is_owner`, não é ligado.
- **Migração real:** em bases já existentes, `is_owner` já foi populado pelo bootstrap do 3a (enquanto `orgRole` existia); dropar a coluna depois disso é seguro (premissa "fase 2 após dados migrados e validados" da spec).
- **`Me_returns_org_and_companies`:** "Admin" ainda apareceria no body via `FullName` ("Admin Demo"), mas a asserção foi trocada para `isOwner`/`permissions` (sinais reais e estáveis), evitando um verde enganoso.

**Ambiente:** build no host; migration e testes via **ferramenta PowerShell** (container). Sem `dotnet ef`/`dotnet test` no host (SAC).

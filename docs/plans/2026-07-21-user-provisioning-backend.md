# Provisionamento de Usuários e Acesso a Empresas — Backend

> **Para agentes:** SUB-SKILL OBRIGATÓRIA: use `superpowers:subagent-driven-development`
> (recomendado) ou `superpowers:executing-plans` para executar tarefa a tarefa.
> Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** entregar `POST /users` (criar usuário) e os três endpoints de
concessão de acesso a empresa, com enforcement, auditoria e testes de integração.

**Arquitetura:** duas capacidades em módulos distintos. Criar usuário entra no
`UserService` existente (`access-control`), usando `UserManager` do Identity
dentro de uma transação explícita para manter o `AuditEvent` atômico. A
concessão de acesso ganha um serviço novo e único, `CompanyAccessService`
(`organizations`), que atende as duas telas — a regra do **escopo concedível**
vive num lugar só.

**Stack:** C# / .NET 10, ASP.NET Core, EF Core + Npgsql, ASP.NET Core Identity,
xUnit + Testcontainers (Postgres real).

**Spec:** `docs/specs/2026-07-21-user-provisioning-company-access-design.md`

**Sem migration.** Nenhuma tabela ou coluna nova: `UserCompanyAccess` já existe,
`Permission` é reconciliada no boot e `ApplicationUser` não muda (a decisão de
produto descartou `must_change_password`). Se em algum momento você achar que
precisa rodar `scripts/ef.ps1`, pare — algo saiu do plano.

## Ambiente

Windows com Smart App Control: `dotnet test` no host é bloqueado. Use sempre o
wrapper em container, com caminho absoluto (exige Docker Desktop):

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1'
```

Para **ver o motivo** de uma falha, rode pelo Bash com redirect — a ferramenta
PowerShell engole o stderr e esconde a mensagem do assert:

```
powershell -NoProfile -File scripts/test.ps1 --filter "FullyQualifiedName~UserProvisioningTests" > /tmp/t.log 2>&1
```

## Estrutura de arquivos

| Arquivo | Responsabilidade |
| --- | --- |
| `apps/api/src/Seed.Application/AccessControl/UserDtos.cs` | + `CreateUserRequest` |
| `apps/api/src/Seed.Application/AccessControl/IUserService.cs` | + `CreateAsync` |
| `apps/api/src/Seed.Application/AccessControl/AccessControlPermissions.cs` | texto de `users.manage` |
| `apps/api/src/Seed.Infrastructure/AccessControl/UserService.cs` | implementação de `CreateAsync` |
| `apps/api/src/Seed.Api/Controllers/UsersController.cs` | `POST /users`, `PUT /users/{id}/companies` |
| `apps/api/src/Seed.Application/Companies/CompaniesPermissions.cs` | + `companies.grant_access` |
| `apps/api/src/Seed.Application/Companies/ICompanyAccessService.cs` | **novo** — contrato, DTOs e exceções |
| `apps/api/src/Seed.Infrastructure/Companies/CompanyAccessService.cs` | **novo** — escopo concedível, delta, auditoria |
| `apps/api/src/Seed.Infrastructure/DependencyInjection.cs` | registro do serviço novo |
| `apps/api/src/Seed.Api/Controllers/CompaniesController.cs` | `GET`/`PUT /companies/{id}/users` |
| `apps/api/tests/Seed.IntegrationTests/UserProvisioningTests.cs` | **novo** — testes de criação |
| `apps/api/tests/Seed.IntegrationTests/CompanyAccessTests.cs` | **novo** — testes de concessão |
| `docs/decisions/ADR-0014-company-access-grant-scope.md` | **novo** |

---

## Task 1: `POST /users` — criar usuário

**Arquivos:**
- Criar: `apps/api/tests/Seed.IntegrationTests/UserProvisioningTests.cs`
- Modificar: `apps/api/src/Seed.Application/AccessControl/UserDtos.cs`
- Modificar: `apps/api/src/Seed.Application/AccessControl/IUserService.cs`
- Modificar: `apps/api/src/Seed.Application/AccessControl/AccessControlPermissions.cs`
- Modificar: `apps/api/src/Seed.Infrastructure/AccessControl/UserService.cs`
- Modificar: `apps/api/src/Seed.Api/Controllers/UsersController.cs`

- [ ] **Passo 1: escrever os testes que falham**

Crie `apps/api/tests/Seed.IntegrationTests/UserProvisioningTests.cs`:

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

// Criação de usuário (POST /users, gate users.manage). Cobre allow-list,
// estado inicial inócuo, mensagem neutra de e-mail duplicado e auditoria.
public class UserProvisioningTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Client logado como um gestor com as permissões dadas (perfil próprio).
    private async Task<HttpClient> ClientWithAsync(string email, params string[] permissionKeys)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
            var now = DateTime.UtcNow;
            var profile = new Profile
            {
                OrganizationId = orgId, Name = $"Perfil {email}", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            foreach (var key in permissionKeys)
                db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = key });
            db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }
        return await factory.CreateLoggedInClientAsync(email, "Passw0rd!");
    }

    [Fact]
    public async Task Create_requires_users_manage()
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync("prov.noperm@demo.local", "Passw0rd!", orgId);
        var client = await factory.CreateLoggedInClientAsync("prov.noperm@demo.local", "Passw0rd!");

        var resp = await client.PostAsJsonAsync("/users", new
        {
            fullName = "Sem Permissão", email = "prov.blocked@demo.local", password = "Passw0rd!",
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Create_returns_201_and_user_starts_with_no_power()
    {
        var manager = await ClientWithAsync("prov.mgr@demo.local", AccessControlPermissions.UsersManage);

        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Maria Silva", email = "prov.maria@demo.local", password = "Passw0rd!",
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(created);
        Assert.Equal("Maria Silva", created!.FullName);
        Assert.Equal(UserStatus.Active.ToString(), created.Status);
        Assert.False(created.IsOwner);
        Assert.Empty(created.Profiles);
        Assert.Empty(created.Companies);

        // A conta nasce inócua: loga, mas /auth/me não traz permissão nem empresa.
        var newbie = await factory.CreateLoggedInClientAsync("prov.maria@demo.local", "Passw0rd!");
        var me = await newbie.GetFromJsonAsync<MeResponse>("/auth/me");
        Assert.NotNull(me);
        Assert.False(me!.IsOwner);
        Assert.Empty(me.Permissions);
        Assert.Empty(me.Companies);
    }

    [Fact]
    public async Task Create_ignores_client_supplied_sensitive_fields()
    {
        var manager = await ClientWithAsync("prov.mass@demo.local", AccessControlPermissions.UsersManage);
        var otherOrg = await factory.CreateSecondTenantAsync(
            orgName: "Org Mass", companyName: "Emp Mass",
            userEmail: "mass@other.local", userPassword: "Mass123!");

        // Campos sensíveis no JSON não existem no DTO — devem ser simplesmente
        // ignorados, jamais aplicados (anti mass-assignment).
        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Tentativa Escalada", email = "prov.escalate@demo.local", password = "Passw0rd!",
            isOwner = true, status = "Inactive", organizationId = otherOrg.OrganizationId,
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<UserDto>();
        Assert.False(created!.IsOwner);
        Assert.Equal(UserStatus.Active.ToString(), created.Status);

        var demoOrgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var stored = await db.Users.FirstAsync(u => u.Email == "prov.escalate@demo.local");
        Assert.Equal(demoOrgId, stored.OrganizationId); // sempre a org do caller
    }

    [Fact]
    public async Task Create_with_duplicate_email_is_400_with_neutral_message()
    {
        var manager = await ClientWithAsync("prov.dup@demo.local", AccessControlPermissions.UsersManage);
        await factory.CreateSecondTenantAsync(
            orgName: "Org Dup", companyName: "Emp Dup",
            userEmail: "prov.taken@other.local", userPassword: "Dup123!");

        // O e-mail existe em OUTRA organização: a resposta não pode revelar isso.
        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Colisão", email = "prov.taken@other.local", password = "Passw0rd!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Org Dup", body);
        Assert.DoesNotContain("já", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_with_weak_password_is_400()
    {
        var manager = await ClientWithAsync("prov.weak@demo.local", AccessControlPermissions.UsersManage);

        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Senha Fraca", email = "prov.weak.target@demo.local", password = "abc",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_emits_audit_without_credentials()
    {
        var manager = await ClientWithAsync("prov.audit@demo.local", AccessControlPermissions.UsersManage);

        var resp = await manager.PostAsJsonAsync("/users", new
        {
            fullName = "Auditado", email = "prov.audited@demo.local", password = "Sup3rSenha!",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<UserDto>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var cid = created!.Id.ToString();
        var ev = await db.AuditEvents.FirstOrDefaultAsync(
            a => a.Action == "access_control.user.created" && a.EntityId == cid);

        Assert.NotNull(ev);
        Assert.Equal("User", ev!.EntityType);
        Assert.NotNull(ev.ActorUserId);
        Assert.Contains("prov.audited@demo.local", ev.Metadata);
        // Nenhum resquício de credencial no metadata (ADR-0013, seção 3).
        Assert.DoesNotContain("Sup3rSenha!", ev.Metadata);
        Assert.DoesNotContain("password", ev.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", ev.Metadata, StringComparison.OrdinalIgnoreCase);
    }

    // Espelho enxuto do payload de /auth/me para desserialização nos testes.
    private record MeResponse(bool IsOwner, List<string> Permissions, List<CompanyRef> Companies);
    private record CompanyRef(Guid Id, string Name);
}
```

- [ ] **Passo 2: rodar e confirmar que falha**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~UserProvisioningTests"
```

Esperado: **erro de compilação** — `IUserService` não tem `CreateAsync` e o
controller não tem `POST`. É a falha correta nesta etapa.

- [ ] **Passo 3: adicionar o DTO de request**

Em `apps/api/src/Seed.Application/AccessControl/UserDtos.cs`, ao final:

```csharp
// Criação de usuário. Allow-list estrita: OrganizationId, IsOwner, Status e
// EmailConfirmed NÃO existem aqui — são fixados pelo servidor, então não há o
// que ignorar. Senha definida pelo administrador (sem convite por e-mail).
public record CreateUserRequest(string FullName, string Email, string Password);
```

- [ ] **Passo 4: declarar o método no contrato**

Em `apps/api/src/Seed.Application/AccessControl/IUserService.cs`, dentro de
`IUserService`, antes de `SetStatusAsync`:

```csharp
    // Cria um usuário na organização do chamador. Nasce Active, sem perfis e sem
    // empresas — permissão efetiva vazia até ser configurado.
    Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct);
```

- [ ] **Passo 5: atualizar o texto da permissão**

Em `apps/api/src/Seed.Application/AccessControl/AccessControlPermissions.cs`,
troque a definição de `UsersManage` (a **chave não muda** — renomear chave
invalida histórico; só o texto exibido muda, e o reconciliador o propaga no boot):

```csharp
        new(UsersManage, Module, "Gerir usuários",
            "Criar, listar, ativar e desativar usuários."),
```

- [ ] **Passo 6: implementar no `UserService`**

Em `apps/api/src/Seed.Infrastructure/AccessControl/UserService.cs`, adicione
`UserManager<ApplicationUser>` ao construtor primário:

```csharp
public class UserService(
    SeedDbContext db, ICurrentUser currentUser, IAuditLog audit,
    UserManager<ApplicationUser> users) : IUserService
```

Adicione os `using` que faltam no topo do arquivo:

```csharp
using Microsoft.AspNetCore.Identity;
using Seed.Infrastructure.Identity;
```

E o método, logo depois de `GetAsync`:

```csharp
    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        var (orgId, _) = await CallerAsync(ct);

        var fullName = (req.FullName ?? "").Trim();
        var email = (req.Email ?? "").Trim();
        if (fullName.Length == 0) throw new UserValidationException("Nome obrigatório.");
        if (email.Length == 0) throw new UserValidationException("E-mail obrigatório.");

        // Transação explícita porque UserManager.CreateAsync chama SaveChanges por
        // conta própria. Sem ela, auditar antes deixaria o evento pendurado no
        // change tracker se a criação falhasse (evento de algo que não aconteceu,
        // proibido pela ADR-0013), e auditar depois permitiria usuário sem evento.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,     // sem e-mail transacional não há confirmação
            FullName = fullName,
            OrganizationId = orgId,    // sempre do chamador, nunca do request
            IsOwner = false,           // owner é gerido fora da aplicação (ADR-0012)
            Status = UserStatus.Active,
        };

        var result = await users.CreateAsync(user, req.Password ?? "");
        if (!result.Succeeded)
        {
            // E-mail é único globalmente. Mensagem neutra para não revelar que a
            // conta existe em OUTRA organização (ver spec, "Riscos aceitos").
            var duplicate = result.Errors.Any(e =>
                e.Code is "DuplicateUserName" or "DuplicateEmail");
            throw new UserValidationException(duplicate
                ? "Não foi possível usar este e-mail."
                : string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        audit.Record(orgId, "access_control.user.created", EntityType, user.Id.ToString(),
            new { full_name = fullName, email });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var list = await BuildAsync(orgId, onlyUserId: user.Id, ct);
        return list[0];
    }
```

- [ ] **Passo 7: expor o endpoint**

Em `apps/api/src/Seed.Api/Controllers/UsersController.cs`, depois do `Get`:

```csharp
    [HttpPost]
    [RequirePermission(AccessControlPermissions.UsersManage)]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct)
    {
        try
        {
            var u = await service.CreateAsync(req, ct);
            return CreatedAtAction(nameof(Get), new { id = u.Id }, u);
        }
        catch (UserValidationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UserForbiddenException) { return Forbid(); }
    }
```

- [ ] **Passo 8: rodar e confirmar que passa**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~UserProvisioningTests"
```

Esperado: **Passed! - Failed: 0, Passed: 6**.

Se `Create_with_duplicate_email_is_400_with_neutral_message` falhar no
`DoesNotContain("já")`, é sinal de que a mensagem do Identity vazou em vez da
neutra — confira a lista de códigos no Passo 6.

- [ ] **Passo 9: rodar a suíte inteira**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1'
```

Esperado: 1 unit + 53 de integração, tudo verde. A suíte antiga não pode
regredir — em especial `UsersTests`, que compartilha o `UserService`.

- [ ] **Passo 10: commit**

```bash
git add apps/api
git commit -m "feat(access-control): POST /users cria usuario sem poder inicial

Usuario nasce Active, sem perfis e sem empresas (permissao efetiva
vazia), com organizationId sempre do chamador. Transacao explicita
mantem o AuditEvent atomico apesar do SaveChanges proprio do
UserManager. E-mail duplicado responde mensagem neutra.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: ADR-0014 e a permissão `companies.grant_access`

**Arquivos:**
- Criar: `docs/decisions/ADR-0014-company-access-grant-scope.md`
- Modificar: `docs/decisions/README.md`
- Modificar: `apps/api/src/Seed.Application/Companies/CompaniesPermissions.cs`
- Criar: `apps/api/tests/Seed.IntegrationTests/CompanyAccessTests.cs`

- [ ] **Passo 1: escrever o teste que falha**

Crie `apps/api/tests/Seed.IntegrationTests/CompanyAccessTests.cs` com apenas o
primeiro teste (os demais entram na Task 3):

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Application.AccessControl;
using Seed.Application.Companies;
using Seed.Domain.AccessControl;
using Seed.Infrastructure.Persistence;

namespace Seed.IntegrationTests;

// Concessão e revogação de acesso a empresa (companies.grant_access) e a regra
// de escopo concedível da ADR-0014.
public class CompanyAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Grant_access_permission_is_reconciled_into_catalog()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();

        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.Key == CompaniesPermissions.GrantAccess);
        Assert.NotNull(permission);
        Assert.Equal(PermissionStatus.Active, permission!.Status);
        Assert.Equal("organizations", permission.Module);

        // O perfil de sistema recebe toda permissão ativa no boot (top-up).
        var orgId = await factory.GetDemoOrganizationIdAsync();
        var systemProfileId = await db.Profiles
            .Where(p => p.OrganizationId == orgId && p.IsSystem).Select(p => p.Id).FirstAsync();
        Assert.True(await db.ProfilePermissions.AnyAsync(pp =>
            pp.ProfileId == systemProfileId && pp.PermissionKey == CompaniesPermissions.GrantAccess));
    }
}
```

- [ ] **Passo 2: rodar e confirmar que falha**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~CompanyAccessTests"
```

Esperado: erro de compilação — `CompaniesPermissions.GrantAccess` não existe.

- [ ] **Passo 3: declarar a permissão**

Em `apps/api/src/Seed.Application/Companies/CompaniesPermissions.cs`, adicione a
constante junto das outras e a definição na lista:

```csharp
    public const string GrantAccess = "companies.grant_access";
```

```csharp
        new(GrantAccess, Module, "Conceder acesso a empresas",
            "Conceder e revogar o acesso de usuários às empresas."),
```

- [ ] **Passo 4: rodar e confirmar que passa**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~CompanyAccessTests"
```

Esperado: **Passed! - Failed: 0, Passed: 1**.

- [ ] **Passo 5: escrever a ADR-0014**

Crie `docs/decisions/ADR-0014-company-access-grant-scope.md`:

```markdown
# ADR-0014: Escopo de Concessão de Acesso a Empresa

## Status

Aceita

## Contexto

A ADR-0012 estabeleceu dois eixos de autorização: o **funcional** (perfis) e o
de **empresa** (`UserCompanyAccess`). Até agora o segundo eixo só era escrito em
dois pontos — a auto-concessão de quem cria a empresa e o seed —, então nunca
houve a pergunta "quem pode conceder acesso a quem".

Com a permissão `companies.grant_access` a pergunta passa a existir, e com ela um
caminho de escalada: sem recorte, quem detém a permissão alcança os dados de
**todas** as empresas da organização simplesmente concedendo acesso a si mesmo.
Isso colapsaria o eixo de empresa dentro do eixo funcional — exatamente o
contrário do que a ADR-0012 decidiu manter independente.

A ADR-0012 adiou a **postura A** ("não conceder além de si") no eixo funcional
por custo: comparar conjuntos de permissões a cada concessão é caro e invasivo.
No eixo de empresa a comparação é um conjunto de ids.

## Decisão

Adotar a **postura A no eixo de empresa**, por meio de um único conceito:

**Escopo concedível do chamador**

- **owner** → todas as empresas ativas da organização;
- **não-owner** → as empresas do próprio `UserCompanyAccess`.

Regras derivadas:

1. Toda empresa citada num pedido de concessão ou revogação precisa estar no
   escopo concedível do chamador. Fora dele a resposta é **404**, e não 403:
   uma empresa da organização à qual o chamador não tem acesso já é hoje
   indistinguível de inexistente (comportamento do `CompanyService`), e um 403
   revelaria sua existência.
2. `PUT /users/{id}/companies` define o conjunto de empresas do usuário **dentro
   do escopo do chamador**. Concessões fora desse escopo são **preservadas**, não
   removidas por ausência no payload. Sem isso a regra se contradiria: a tela do
   chamador só lista o que ele pode conceder.
3. O **owner é isento** por ter a organização inteira como escopo. É o mesmo
   piso antilockout da ADR-0012, e é o que destrava uma **empresa órfã** — aquela
   cujo único usuário com acesso foi desativado.
4. O **owner alvo** pode ter suas empresas alteradas, ao contrário de status e
   perfis, onde é somente-leitura. Ele está sujeito ao eixo de empresa, então
   precisa poder receber acesso, e sempre consegue se reconceder.

O eixo funcional **permanece em postura B**: `profiles.manage` e
`profiles.assign` continuam privilégios administrativos de fato, com perfis
`is_system` restritos ao owner.

## Consequências

- `companies.grant_access` não vale, na prática, "acesso a todos os dados da
  organização": o alcance de quem a detém é limitado pelo próprio acesso.
- Um administrador que não acessa nenhuma empresa não concede nenhuma. É o
  resultado pretendido; o owner é o caminho de destravamento.
- Nenhuma migração de dados: a chave nova entra pelo reconciliador de catálogo no
  boot e é concedida ao perfil de sistema pelo top-up do bootstrapper.
- O projeto passa a ter duas posturas convivendo — A no eixo de empresa, B no
  funcional. A assimetria é deliberada e vem do custo de implementação, não de
  uma diferença de risco; a evolução do eixo funcional para a postura A segue no
  backlog.

## Alternativas Consideradas

### Sem recorte (qualquer empresa da organização)

Rejeitada. Implementação mais simples, mas transforma `companies.grant_access`
em acesso universal aos dados por autoconcessão, anulando o segundo eixo.

### Recorte sem isenção do owner

Rejeitada. Regra uniforme, sem caso especial, mas deixa uma empresa órfã
inalcançável pela aplicação — e o owner, que existe justamente para ser o piso
antilockout, precisaria do banco para destravá-la.

### Responder 403 para empresa fora do escopo

Rejeitada. Mensagem mais clara ao operador, mas revela a existência de empresas
que ele hoje não consegue distinguir de inexistentes, criando um vazamento que o
resto do módulo não tem.

## Validação

Esta decisão permanece válida se:

- nenhum não-owner conceder ou revogar empresa fora do próprio acesso;
- empresa fora do escopo continuar respondendo 404, nunca 403;
- concessões fora do escopo do chamador forem preservadas em vez de removidas;
- o owner mantiver escopo total e continuar capaz de destravar empresa órfã;
- as regras permanecerem cobertas por teste de integração.

## Decisões Relacionadas

- ADR-0012 (perfis configuráveis, dois eixos, postura B) — decisão que esta
  refina no eixo de empresa.
- ADR-0010 (modelo multiempresa, origem do `UserCompanyAccess`).
- ADR-0013 (padrão do `AuditEvent`) — contrato dos eventos de concessão.
- Design: `docs/specs/2026-07-21-user-provisioning-company-access-design.md`.
```

- [ ] **Passo 6: indexar a ADR**

Em `docs/decisions/README.md`, adicione a linha ao final da tabela:

```markdown
| [ADR-0014](ADR-0014-company-access-grant-scope.md) | Aceita | Escopo de concessao de acesso a empresa: postura A no eixo de empresa (so concede o que acessa), owner isento |
```

- [ ] **Passo 7: commit**

```bash
git add apps/api docs/decisions
git commit -m "feat(organizations): permissao companies.grant_access e ADR-0014

Declara a chave nova (reconciliada no boot, sem migration) e registra a
decisao de escopo concedivel: postura A no eixo de empresa, owner isento,
empresa fora do escopo responde 404 para nao vazar existencia.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: `CompanyAccessService` e `PUT /users/{id}/companies`

**Arquivos:**
- Criar: `apps/api/src/Seed.Application/Companies/ICompanyAccessService.cs`
- Criar: `apps/api/src/Seed.Infrastructure/Companies/CompanyAccessService.cs`
- Modificar: `apps/api/src/Seed.Infrastructure/DependencyInjection.cs`
- Modificar: `apps/api/src/Seed.Api/Controllers/UsersController.cs`
- Modificar: `apps/api/tests/Seed.IntegrationTests/CompanyAccessTests.cs`

- [ ] **Passo 1: escrever os testes que falham**

Em `apps/api/tests/Seed.IntegrationTests/CompanyAccessTests.cs`, adicione os
helpers e os testes abaixo dentro da classe existente:

```csharp
    // Cria um usuário na org Demo e devolve seu id (sem perfil, sem empresa).
    private async Task<Guid> CreateMemberAsync(string email)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        await factory.CreateUserAsync(email, "Passw0rd!", orgId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }

    // Client logado com as permissões dadas.
    private async Task<HttpClient> ClientWithAsync(string email, params string[] permissionKeys)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        var userId = await CreateMemberAsync(email);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            var profile = new Profile
            {
                OrganizationId = orgId, Name = $"Perfil {email}", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            foreach (var key in permissionKeys)
                db.ProfilePermissions.Add(new ProfilePermission { ProfileId = profile.Id, PermissionKey = key });
            db.UserProfiles.Add(new UserProfile { UserId = userId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }
        return await factory.CreateLoggedInClientAsync(email, "Passw0rd!");
    }

    // Cria uma empresa na org Demo e, opcionalmente, concede acesso a alguém.
    private async Task<Guid> CreateCompanyAsync(string name, Guid? grantTo = null)
    {
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var now = DateTime.UtcNow;
        var company = new Seed.Domain.Companies.Company
        {
            OrganizationId = orgId, Name = name, CreatedAt = now, UpdatedAt = now,
        };
        db.Companies.Add(company);
        if (grantTo is not null)
            db.UserCompanyAccesses.Add(new Seed.Domain.Access.UserCompanyAccess
            {
                UserId = grantTo.Value, CompanyId = company.Id, OrganizationId = orgId,
                CreatedAt = now, UpdatedAt = now,
            });
        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task<List<Guid>> CompaniesOfAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        return await db.UserCompanyAccesses.Where(a => a.UserId == userId)
            .Select(a => a.CompanyId).ToListAsync();
    }

    [Fact]
    public async Task Set_user_companies_requires_grant_access_permission()
    {
        // Tem users.manage, mas não companies.grant_access.
        var client = await ClientWithAsync("acc.noperm@demo.local", AccessControlPermissions.UsersManage);
        var targetId = await CreateMemberAsync("acc.noperm.target@demo.local");
        var companyId = await CreateCompanyAsync("Emp NoPerm");

        var resp = await client.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Owner_grants_and_revokes_any_company()
    {
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.target@demo.local");
        // Empresa órfã: ninguém tem acesso a ela.
        var orphanId = await CreateCompanyAsync("Emp Orfa");

        var grant = await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { orphanId } });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        Assert.Contains(orphanId, await CompaniesOfAsync(targetId));

        var revoke = await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Empty(await CompaniesOfAsync(targetId));
    }

    [Fact]
    public async Task Non_owner_cannot_grant_company_outside_own_scope()
    {
        var granterId = await CreateMemberAsync("acc.granter@demo.local");
        var mine = await CreateCompanyAsync("Emp Minha", grantTo: granterId);
        var outside = await CreateCompanyAsync("Emp Fora");
        var granter = await factory.CreateLoggedInClientAsync("acc.granter@demo.local", "Passw0rd!");

        // Dá a permissão ao granter (já criado acima) sem tocar nas empresas.
        var orgId = await factory.GetDemoOrganizationIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            var profile = new Profile
            {
                OrganizationId = orgId, Name = "Perfil Granter", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            db.ProfilePermissions.Add(new ProfilePermission
            {
                ProfileId = profile.Id, PermissionKey = CompaniesPermissions.GrantAccess,
            });
            db.UserProfiles.Add(new UserProfile { UserId = granterId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        var targetId = await CreateMemberAsync("acc.scope.target@demo.local");

        // Dentro do escopo: concede.
        var ok = await granter.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { mine } });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // Fora do escopo: 404 (não revela que a empresa existe).
        var denied = await granter.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { mine, outside } });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task Grants_outside_caller_scope_are_preserved()
    {
        var granterId = await CreateMemberAsync("acc.preserve.granter@demo.local");
        var mine = await CreateCompanyAsync("Emp Preserva Minha", grantTo: granterId);
        var outside = await CreateCompanyAsync("Emp Preserva Fora");
        var targetId = await CreateMemberAsync("acc.preserve.target@demo.local");

        var orgId = await factory.GetDemoOrganizationIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            // O alvo já tem a empresa que o granter não enxerga.
            db.UserCompanyAccesses.Add(new Seed.Domain.Access.UserCompanyAccess
            {
                UserId = targetId, CompanyId = outside, OrganizationId = orgId,
                CreatedAt = now, UpdatedAt = now,
            });
            var profile = new Profile
            {
                OrganizationId = orgId, Name = "Perfil Preserva", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            db.ProfilePermissions.Add(new ProfilePermission
            {
                ProfileId = profile.Id, PermissionKey = CompaniesPermissions.GrantAccess,
            });
            db.UserProfiles.Add(new UserProfile { UserId = granterId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        var granter = await factory.CreateLoggedInClientAsync("acc.preserve.granter@demo.local", "Passw0rd!");

        // Envia só o que enxerga. A concessão fora do escopo NÃO pode sumir.
        var resp = await granter.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { mine } });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var companies = await CompaniesOfAsync(targetId);
        Assert.Contains(mine, companies);
        Assert.Contains(outside, companies);
    }

    [Fact]
    public async Task Cross_tenant_target_and_company_are_404()
    {
        var owner = await factory.CreateAdminClientAsync();
        var other = await factory.CreateSecondTenantAsync(
            orgName: "Org Acc", companyName: "Emp Acc",
            userEmail: "acc@other.local", userPassword: "Acc123!");
        var localTarget = await CreateMemberAsync("acc.cross.target@demo.local");

        Guid foreignUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            foreignUserId = await db.Users.Where(u => u.Email == "acc@other.local")
                .Select(u => u.Id).FirstAsync();
        }

        // Usuário de outra organização como alvo.
        var badTarget = await owner.PutAsJsonAsync($"/users/{foreignUserId}/companies",
            new { companyIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.NotFound, badTarget.StatusCode);

        // Empresa de outra organização no payload.
        var badCompany = await owner.PutAsJsonAsync($"/users/{localTarget}/companies",
            new { companyIds = new[] { other.CompanyId } });
        Assert.Equal(HttpStatusCode.NotFound, badCompany.StatusCode);
    }

    [Fact]
    public async Task Soft_deleted_company_is_404()
    {
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.deleted.target@demo.local");

        Guid ownerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            ownerId = await db.Users.Where(u => u.Email == ApiFactory.AdminEmail)
                .Select(u => u.Id).FirstAsync();
        }

        // Concedida ao próprio owner: sem acesso, o DELETE responderia 404 e o
        // teste passaria pelo motivo errado (empresa nunca excluída).
        var companyId = await CreateCompanyAsync("Emp Excluida", grantTo: ownerId);
        var deleted = await owner.DeleteAsync($"/companies/{companyId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Exclusão é soft (DeletedAt). O filtro global de Company a tira do
        // escopo concedível, então ela some como se nunca tivesse existido.
        var resp = await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Owner_target_can_have_companies_changed()
    {
        var owner = await factory.CreateAdminClientAsync();
        var companyId = await CreateCompanyAsync("Emp Do Owner");

        Guid ownerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            ownerId = await db.Users.Where(u => u.Email == ApiFactory.AdminEmail)
                .Select(u => u.Id).FirstAsync();
        }

        var current = await CompaniesOfAsync(ownerId);
        var desired = current.Append(companyId).ToArray();

        // Ao contrário de status e perfis, o eixo de empresa é editável no owner.
        var resp = await owner.PutAsJsonAsync($"/users/{ownerId}/companies",
            new { companyIds = desired });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains(companyId, await CompaniesOfAsync(ownerId));
    }

    [Fact]
    public async Task Grant_and_revoke_emit_audit_events()
    {
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.audit.target@demo.local");
        var companyId = await CreateCompanyAsync("Emp Auditada");

        await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });
        await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = Array.Empty<Guid>() });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
        var tid = targetId.ToString();

        var granted = await db.AuditEvents.FirstOrDefaultAsync(a =>
            a.Action == "organizations.user.company_access_granted" && a.EntityId == tid);
        var revoked = await db.AuditEvents.FirstOrDefaultAsync(a =>
            a.Action == "organizations.user.company_access_revoked" && a.EntityId == tid);

        Assert.NotNull(granted);
        Assert.NotNull(revoked);
        Assert.Equal("User", granted!.EntityType);
        // Rótulo humano junto do id (ADR-0013, seção 3).
        Assert.Contains("Emp Auditada", granted.Metadata);
        Assert.Contains("Emp Auditada", revoked!.Metadata);
    }

    [Fact]
    public async Task Company_axis_still_requires_companies_access_permission()
    {
        // Os dois eixos continuam independentes: ter UserCompanyAccess sem a
        // permissão funcional companies.access não dá acesso à listagem.
        var owner = await factory.CreateAdminClientAsync();
        var targetId = await CreateMemberAsync("acc.twoaxis@demo.local");
        var companyId = await CreateCompanyAsync("Emp Dois Eixos");

        await owner.PutAsJsonAsync($"/users/{targetId}/companies",
            new { companyIds = new[] { companyId } });

        var target = await factory.CreateLoggedInClientAsync("acc.twoaxis@demo.local", "Passw0rd!");
        var resp = await target.GetAsync("/companies");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
```

- [ ] **Passo 2: rodar e confirmar que falha**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~CompanyAccessTests"
```

Esperado: erro de compilação (o endpoint não existe) ou, se compilar, 404/405 em
todos os `PUT`.

- [ ] **Passo 3: criar o contrato do serviço**

Crie `apps/api/src/Seed.Application/Companies/ICompanyAccessService.cs`:

```csharp
namespace Seed.Application.Companies;

// Alvo (usuário ou empresa) fora da organização do chamador, ou empresa fora do
// seu escopo concedível (ADR-0014). → 404, sem vazar existência.
public class CompanyAccessNotFoundException(string message) : Exception(message);

// Conflito de concorrência ao aplicar a mutação (índice único (UserId,
// CompanyId) ou remoção simultânea da mesma linha). → 409.
public class CompanyAccessConflictException(string message) : Exception(message);

// Usuário da organização, com a marca de quem já tem acesso à empresa em foco.
public record CompanyUserAccessDto(Guid Id, string FullName, string Email, bool HasAccess);

// Requests (allow-list — organização e ator vêm sempre da sessão).
public record SetUserCompaniesRequest(IReadOnlyList<Guid>? CompanyIds);
public record SetCompanyUsersRequest(IReadOnlyList<Guid>? UserIds);

// Concessão e revogação de acesso a empresa (o eixo de dados da ADR-0012).
// Serviço único por trás das duas telas; a regra de escopo concedível da
// ADR-0014 vive aqui, não nos controllers.
public interface ICompanyAccessService
{
    // Define as empresas do usuário DENTRO do escopo concedível do chamador.
    // Concessões fora desse escopo são preservadas (ADR-0014, regra 2).
    Task SetUserCompaniesAsync(Guid userId, SetUserCompaniesRequest req, CancellationToken ct);

    // Usuários da organização, marcando quem tem acesso à empresa.
    Task<IReadOnlyList<CompanyUserAccessDto>> ListCompanyUsersAsync(Guid companyId, CancellationToken ct);

    // Define o conjunto de usuários com acesso à empresa.
    Task SetCompanyUsersAsync(Guid companyId, SetCompanyUsersRequest req, CancellationToken ct);
}
```

- [ ] **Passo 4: implementar o serviço**

Crie `apps/api/src/Seed.Infrastructure/Companies/CompanyAccessService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Seed.Application.Abstractions;
using Seed.Application.Audit;
using Seed.Application.Companies;
using Seed.Domain.Access;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.Companies;

// Concessão de acesso a empresa. A autorização funcional (companies.grant_access)
// é feita no gate do controller; aqui mora a tenancy e o escopo concedível da
// ADR-0014, que é a trava contra autoconcessão a empresas alheias.
public class CompanyAccessService(
    SeedDbContext db, ICurrentUser currentUser, IClock clock, IAuditLog audit) : ICompanyAccessService
{
    private const string EntityType = "User";

    private async Task<(Guid OrgId, Guid CallerId, bool IsOwner)> CallerAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new CompanyAccessNotFoundException("Não autenticado.");
        var caller = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.OrganizationId, u.IsOwner })
            .FirstOrDefaultAsync(ct)
            ?? throw new CompanyAccessNotFoundException("Usuário sem organização.");
        return (caller.OrganizationId, userId, caller.IsOwner);
    }

    // Escopo concedível (ADR-0014): owner alcança toda a organização; os demais,
    // apenas as empresas do próprio acesso. O filtro global de Company já exclui
    // as soft-deleted, então elas ficam fora do escopo por construção.
    private async Task<HashSet<Guid>> GrantableScopeAsync(
        Guid orgId, Guid callerId, bool isOwner, CancellationToken ct)
    {
        if (isOwner)
            return (await db.Companies.Where(c => c.OrganizationId == orgId)
                .Select(c => c.Id).ToListAsync(ct)).ToHashSet();

        return (await (
            from a in db.UserCompanyAccesses
            join c in db.Companies on a.CompanyId equals c.Id
            where a.UserId == callerId && a.OrganizationId == orgId
            select c.Id).ToListAsync(ct)).ToHashSet();
    }

    public async Task SetUserCompaniesAsync(Guid userId, SetUserCompaniesRequest req, CancellationToken ct)
    {
        var (orgId, callerId, isOwner) = await CallerAsync(ct);

        var targetExists = await db.Users.AnyAsync(u => u.Id == userId && u.OrganizationId == orgId, ct);
        if (!targetExists)
            throw new CompanyAccessNotFoundException("Usuário inexistente nesta organização.");

        var scope = await GrantableScopeAsync(orgId, callerId, isOwner, ct);
        var requested = (req.CompanyIds ?? []).Distinct().ToList();

        // Empresa fora do escopo é indistinguível de inexistente (ADR-0014).
        if (requested.Any(id => !scope.Contains(id)))
            throw new CompanyAccessNotFoundException("Empresa inexistente nesta organização.");

        var current = await db.UserCompanyAccesses
            .Where(a => a.UserId == userId && a.OrganizationId == orgId)
            .Select(a => a.CompanyId).ToListAsync(ct);

        // Só o que está no escopo entra no cálculo de remoção: o que o chamador
        // não enxerga é preservado, não removido por ausência no payload.
        var currentInScope = current.Where(scope.Contains).ToList();

        var toAdd = requested.Except(current).ToList();
        var toRemove = currentInScope.Except(requested).ToList();

        await ApplyAsync(orgId, userId, toAdd, toRemove, ct);
    }

    public async Task<IReadOnlyList<CompanyUserAccessDto>> ListCompanyUsersAsync(
        Guid companyId, CancellationToken ct)
    {
        var (orgId, callerId, isOwner) = await CallerAsync(ct);
        var scope = await GrantableScopeAsync(orgId, callerId, isOwner, ct);
        if (!scope.Contains(companyId))
            throw new CompanyAccessNotFoundException("Empresa inexistente nesta organização.");

        var granted = await db.UserCompanyAccesses
            .Where(a => a.CompanyId == companyId && a.OrganizationId == orgId)
            .Select(a => a.UserId).ToListAsync(ct);
        var grantedSet = granted.ToHashSet();

        var users = await db.Users.Where(u => u.OrganizationId == orgId)
            .OrderBy(u => u.FullName).ThenBy(u => u.Email)
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync(ct);

        return users
            .Select(u => new CompanyUserAccessDto(u.Id, u.FullName, u.Email ?? "", grantedSet.Contains(u.Id)))
            .ToList();
    }

    public async Task SetCompanyUsersAsync(Guid companyId, SetCompanyUsersRequest req, CancellationToken ct)
    {
        var (orgId, callerId, isOwner) = await CallerAsync(ct);
        var scope = await GrantableScopeAsync(orgId, callerId, isOwner, ct);
        if (!scope.Contains(companyId))
            throw new CompanyAccessNotFoundException("Empresa inexistente nesta organização.");

        var requested = (req.UserIds ?? []).Distinct().ToList();
        var validCount = requested.Count == 0 ? 0 : await db.Users
            .CountAsync(u => requested.Contains(u.Id) && u.OrganizationId == orgId, ct);
        if (validCount != requested.Count)
            throw new CompanyAccessNotFoundException("Usuário inexistente nesta organização.");

        var current = await db.UserCompanyAccesses
            .Where(a => a.CompanyId == companyId && a.OrganizationId == orgId)
            .Select(a => a.UserId).ToListAsync(ct);

        // A empresa está no escopo, então todos os usuários da organização são
        // alvo legítimo: aqui o conjunto é completo, sem preservação.
        foreach (var uid in requested.Except(current))
            await ApplyAsync(orgId, uid, [companyId], [], ct, save: false);
        foreach (var uid in current.Except(requested))
            await ApplyAsync(orgId, uid, [], [companyId], ct, save: false);

        await SaveAsync(ct);
    }

    // Aplica o delta de UM usuário. save: false acumula no change tracker para
    // que o chamador persista tudo numa única unidade de trabalho (ADR-0013).
    private async Task ApplyAsync(
        Guid orgId, Guid userId, List<Guid> toAdd, List<Guid> toRemove,
        CancellationToken ct, bool save = true)
    {
        var touched = toAdd.Concat(toRemove).Distinct().ToList();
        if (touched.Count == 0)
        {
            if (save) await SaveAsync(ct);
            return;
        }

        var names = await db.Companies.Where(c => touched.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var now = clock.UtcNow;

        foreach (var cid in toAdd)
        {
            db.UserCompanyAccesses.Add(new UserCompanyAccess
            {
                UserId = userId, CompanyId = cid, OrganizationId = orgId,
                CreatedAt = now, UpdatedAt = now,
            });
            audit.Record(orgId, "organizations.user.company_access_granted", EntityType,
                userId.ToString(),
                new
                {
                    company_id = cid, company_name = names.GetValueOrDefault(cid),
                    old = false, @new = true,
                });
        }

        foreach (var cid in toRemove)
        {
            var row = await db.UserCompanyAccesses.FirstOrDefaultAsync(
                a => a.UserId == userId && a.CompanyId == cid && a.OrganizationId == orgId, ct);
            if (row is null) continue; // já removida concorrentemente
            db.UserCompanyAccesses.Remove(row);
            audit.Record(orgId, "organizations.user.company_access_revoked", EntityType,
                userId.ToString(),
                new
                {
                    company_id = cid, company_name = names.GetValueOrDefault(cid),
                    old = true, @new = false,
                });
        }

        if (save) await SaveAsync(ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Corrida de concessão idêntica: o índice único (UserId, CompanyId)
            // barrou a segunda inserção.
            throw new CompanyAccessConflictException("Conflito ao atualizar os acessos. Tente novamente.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CompanyAccessConflictException("Conflito ao atualizar os acessos. Tente novamente.");
        }
    }
}
```

- [ ] **Passo 5: registrar no contêiner**

Em `apps/api/src/Seed.Infrastructure/DependencyInjection.cs`, junto dos outros
`AddScoped`:

```csharp
        s.AddScoped<ICompanyAccessService, Companies.CompanyAccessService>();
```

- [ ] **Passo 6: expor `PUT /users/{id}/companies`**

Em `apps/api/src/Seed.Api/Controllers/UsersController.cs`, injete o serviço novo
no construtor primário:

```csharp
public class UsersController(IUserService service, ICompanyAccessService companyAccess) : ControllerBase
```

Adicione o `using`:

```csharp
using Seed.Application.Companies;
```

E o endpoint, ao final da classe:

```csharp
    // Eixo de empresa: gate próprio (companies.grant_access), distinto de
    // users.manage. O conjunto vale DENTRO do escopo concedível (ADR-0014).
    [HttpPut("{id:guid}/companies")]
    [RequirePermission(CompaniesPermissions.GrantAccess)]
    public async Task<IActionResult> SetCompanies(Guid id, SetUserCompaniesRequest req, CancellationToken ct)
    {
        try
        {
            await companyAccess.SetUserCompaniesAsync(id, req, ct);
            var u = await service.GetAsync(id, ct);
            return u is null ? NotFound() : Ok(u);
        }
        catch (CompanyAccessNotFoundException) { return NotFound(); }
        catch (CompanyAccessConflictException ex) { return Conflict(new { error = ex.Message }); }
    }
```

- [ ] **Passo 7: rodar e confirmar que passa**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~CompanyAccessTests"
```

Esperado: **Passed! - Failed: 0, Passed: 10**.

Se `Grants_outside_caller_scope_are_preserved` falhar, o cálculo de `toRemove`
está usando `current` em vez de `currentInScope` — é o erro que a regra 2 da
ADR-0014 existe para evitar.

- [ ] **Passo 8: commit**

```bash
git add apps/api
git commit -m "feat(organizations): concessao de acesso a empresa por usuario

CompanyAccessService centraliza a regra de escopo concedivel (ADR-0014)
e a auditoria; PUT /users/{id}/companies preserva concessoes fora do
escopo do chamador em vez de remove-las por ausencia no payload.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: `GET` e `PUT /companies/{id}/users`

**Arquivos:**
- Modificar: `apps/api/src/Seed.Api/Controllers/CompaniesController.cs`
- Modificar: `apps/api/tests/Seed.IntegrationTests/CompanyAccessTests.cs`

- [ ] **Passo 1: escrever os testes que falham**

Adicione à classe `CompanyAccessTests`:

```csharp
    [Fact]
    public async Task List_company_users_marks_who_has_access()
    {
        var owner = await factory.CreateAdminClientAsync();
        var withAccess = await CreateMemberAsync("acc.list.in@demo.local");
        await CreateMemberAsync("acc.list.out@demo.local");
        var companyId = await CreateCompanyAsync("Emp Listagem", grantTo: withAccess);

        var users = await owner.GetFromJsonAsync<List<CompanyUserAccessDto>>($"/companies/{companyId}/users");

        Assert.NotNull(users);
        var inUser = users!.First(u => u.Email == "acc.list.in@demo.local");
        var outUser = users.First(u => u.Email == "acc.list.out@demo.local");
        Assert.True(inUser.HasAccess);
        Assert.False(outUser.HasAccess);
    }

    [Fact]
    public async Task List_company_users_is_404_outside_caller_scope()
    {
        var granterId = await CreateMemberAsync("acc.list.granter@demo.local");
        var outside = await CreateCompanyAsync("Emp Lista Fora");

        var orgId = await factory.GetDemoOrganizationIdAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeedDbContext>();
            var now = DateTime.UtcNow;
            var profile = new Profile
            {
                OrganizationId = orgId, Name = "Perfil Lista", Status = ProfileStatus.Active,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Profiles.Add(profile);
            db.ProfilePermissions.Add(new ProfilePermission
            {
                ProfileId = profile.Id, PermissionKey = CompaniesPermissions.GrantAccess,
            });
            db.UserProfiles.Add(new UserProfile { UserId = granterId, ProfileId = profile.Id });
            await db.SaveChangesAsync();
        }

        var granter = await factory.CreateLoggedInClientAsync("acc.list.granter@demo.local", "Passw0rd!");
        var resp = await granter.GetAsync($"/companies/{outside}/users");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Set_company_users_replaces_the_set()
    {
        var owner = await factory.CreateAdminClientAsync();
        var first = await CreateMemberAsync("acc.set.first@demo.local");
        var second = await CreateMemberAsync("acc.set.second@demo.local");
        var companyId = await CreateCompanyAsync("Emp Conjunto", grantTo: first);

        var resp = await owner.PutAsJsonAsync($"/companies/{companyId}/users",
            new { userIds = new[] { second } });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        Assert.DoesNotContain(companyId, await CompaniesOfAsync(first));
        Assert.Contains(companyId, await CompaniesOfAsync(second));
    }

    [Fact]
    public async Task Set_company_users_requires_grant_access_permission()
    {
        var client = await ClientWithAsync("acc.set.noperm@demo.local", CompaniesPermissions.Manage);
        var companyId = await CreateCompanyAsync("Emp Set NoPerm");

        var resp = await client.PutAsJsonAsync($"/companies/{companyId}/users",
            new { userIds = Array.Empty<Guid>() });

        // companies.manage NÃO habilita conceder acesso — são gates distintos.
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
```

- [ ] **Passo 2: rodar e confirmar que falha**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~CompanyAccessTests"
```

Esperado: os quatro testes novos falham com `404 NotFound` (rota inexistente) ou
erro de compilação.

- [ ] **Passo 3: expor os endpoints**

Em `apps/api/src/Seed.Api/Controllers/CompaniesController.cs`, injete o serviço:

```csharp
public class CompaniesController(
    ICompanyService service, ICompanyAccessService companyAccess) : ControllerBase
```

E adicione ao final da classe:

```csharp
    // Quem tem acesso a esta empresa. Gate companies.grant_access (não
    // companies.manage): conceder acesso a dados e manter cadastro são poderes
    // de natureza diferente. Devolve o mínimo para escolher — id, nome e e-mail.
    [HttpGet("{id:guid}/users")]
    [RequirePermission(CompaniesPermissions.GrantAccess)]
    public async Task<IActionResult> ListUsers(Guid id, CancellationToken ct)
    {
        try { return Ok(await companyAccess.ListCompanyUsersAsync(id, ct)); }
        catch (CompanyAccessNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/users")]
    [RequirePermission(CompaniesPermissions.GrantAccess)]
    public async Task<IActionResult> SetUsers(Guid id, SetCompanyUsersRequest req, CancellationToken ct)
    {
        try
        {
            await companyAccess.SetCompanyUsersAsync(id, req, ct);
            return NoContent();
        }
        catch (CompanyAccessNotFoundException) { return NotFound(); }
        catch (CompanyAccessConflictException ex) { return Conflict(new { error = ex.Message }); }
    }
```

- [ ] **Passo 4: rodar e confirmar que passa**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1' --filter "FullyQualifiedName~CompanyAccessTests"
```

Esperado: **Passed! - Failed: 0, Passed: 14**.

- [ ] **Passo 5: rodar a suíte inteira**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1'
```

Esperado: 1 unit + 67 de integração, tudo verde. Se o total divergir, confira se
algum teste antigo quebrou antes de ajustar o número aqui.

- [ ] **Passo 6: commit**

```bash
git add apps/api
git commit -m "feat(organizations): gestao de acesso pela tela da empresa

GET/PUT /companies/{id}/users sobre o mesmo CompanyAccessService. Gate
companies.grant_access, distinto de companies.manage; empresa fora do
escopo concedivel responde 404.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Revisão de segurança (obrigatória antes do frontend)

- [ ] **Passo 1: rodar a skill `security-engineer`** sobre o diff da branch,
      tratando explicitamente: escalada de privilégio pela criação de conta,
      autoconcessão de acesso a empresa, isolamento cross-tenant nos quatro
      endpoints novos, mass-assignment nos DTOs e ausência de credencial na
      auditoria.

- [ ] **Passo 2: registrar o resultado.** Achado aceito vira correção com teste;
      achado recusado vira nota na seção "Riscos aceitos" da spec, com o motivo.

## Próximo plano

`docs/plans/2026-07-21-user-provisioning-frontend.md` — as três telas, os testes
unitários, o e2e e a atualização das docs de módulo.

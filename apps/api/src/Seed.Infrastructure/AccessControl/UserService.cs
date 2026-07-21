using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Seed.Application.Abstractions;
using Seed.Application.AccessControl;
using Seed.Application.Audit;
using Seed.Domain.AccessControl;
using Seed.Domain.Organizations;
using Seed.Infrastructure.Identity;
using Seed.Infrastructure.Persistence;

namespace Seed.Infrastructure.AccessControl;

// Gestão de usuários da organização da sessão. Consistente com ProfileService:
// DbContext direto, tenancy resolvida pelo usuário atual, auditoria na mesma UoW.
public class UserService(
    SeedDbContext db, ICurrentUser currentUser, IAuditLog audit,
    UserManager<ApplicationUser> users) : IUserService
{
    private const string EntityType = "User";

    // Resposta única para qualquer colisão de e-mail, venha ela do validador do
    // Identity ou do índice único sob corrida. Precisa ser a MESMA nos dois
    // caminhos: divergir aqui transforma a criação num oráculo capaz de
    // distinguir "conta existe em outra organização" de "e-mail livre".
    public const string DuplicateEmailMessage = "Não foi possível usar este e-mail.";

    // Mesmo limite de Profile.Name, Organization.Name e Company.Name. Aplicado no
    // serviço porque a coluna FullName ainda é text sem HasMaxLength — apertar o
    // esquema é decisão própria, com migration. Sem isso, um nome arbitrariamente
    // longo entraria por request e seria copiado para AuditEvents, append-only.
    private const int FullNameMaxLength = 200;

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

    public async Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        var (orgId, _) = await CallerAsync(ct);

        var fullName = (req.FullName ?? "").Trim();
        var email = (req.Email ?? "").Trim();
        if (fullName.Length == 0) throw new UserValidationException("Nome obrigatório.");
        if (fullName.Length > FullNameMaxLength)
            throw new UserValidationException(
                $"Nome deve ter no máximo {FullNameMaxLength} caracteres.");
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

        // UserManager.CreateAsync não tem sobrecarga com CancellationToken; o ct
        // segue honrado nas leituras e no SaveChanges em volta.
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, req.Password ?? "");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // O UserValidator detecta duplicidade com um SELECT prévio que não é
            // atômico com o INSERT: sob concorrência as duas requisições passam a
            // checagem e a segunda só colide no índice único (UserNameIndex),
            // como exceção. Traduz para a mesma resposta do caminho serial em vez
            // de deixar vazar como 500.
            throw new UserValidationException(DuplicateEmailMessage);
        }

        if (!result.Succeeded)
        {
            // E-mail é único globalmente. Mensagem neutra para não revelar que a
            // conta existe em OUTRA organização (ver spec, "Riscos aceitos").
            var duplicate = result.Errors.Any(e =>
                e.Code is "DuplicateUserName" or "DuplicateEmail");
            throw new UserValidationException(duplicate
                ? DuplicateEmailMessage
                : string.Join(" ", result.Errors.Select(Translate).Distinct()));
        }

        audit.Record(orgId, "access_control.user.created", EntityType, user.Id.ToString(),
            new { full_name = fullName, email });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Montado em memória: o usuário nasce sem perfil e sem empresa, então o
        // conteúdo é determinístico. Evita reler o banco com a transação já
        // encerrada e o list[0] sem guarda.
        return new UserDto(user.Id, fullName, email, UserStatus.Active.ToString(), false, [], []);
    }

    // Mensagens do Identity vêm em inglês e a de InvalidEmail ecoa o input do
    // chamador; por isso a descrição original nunca é devolvida. Fallback
    // genérico para todo código não mapeado.
    private static string Translate(IdentityError e) => e.Code switch
    {
        "PasswordTooShort" => "A senha deve ter ao menos 8 caracteres.",
        "PasswordRequiresDigit" => "A senha deve conter ao menos um número.",
        "PasswordRequiresUpper" => "A senha deve conter ao menos uma letra maiúscula.",
        "PasswordRequiresLower" => "A senha deve conter ao menos uma letra minúscula.",
        "PasswordRequiresNonAlphanumeric" => "A senha deve conter ao menos um símbolo.",
        "InvalidEmail" or "InvalidUserName" => "E-mail inválido.",
        _ => "Não foi possível criar o usuário.",
    };

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
        return list.Count == 0 ? null : list[0];
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
        var requestedInOrgCount = requested.Count == 0
            ? 0
            : await db.Profiles.CountAsync(p => requested.Contains(p.Id) && p.OrganizationId == orgId, ct);
        if (requestedInOrgCount != requested.Count)
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
                new { profile_id = pid, profile_name = meta.TryGetValue(pid, out var m) ? m.Name : null, old = false, @new = true });
        }
        foreach (var pid in toRemove)
        {
            // Remove por entidade-stub (PK composta), sem SELECT prévio — evita o
            // N+1 e o FirstAsync que estouraria sob remoção concorrente.
            db.UserProfiles.Remove(new UserProfile { UserId = id, ProfileId = pid });
            audit.Record(orgId, "access_control.user.profile_removed", EntityType, id.ToString(),
                new { profile_id = pid, profile_name = meta.TryGetValue(pid, out var m) ? m.Name : null, old = true, @new = false });
        }

        if (toAdd.Count > 0 || toRemove.Count > 0)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                // Corrida de atribuição idêntica: outra requisição concorrente já
                // inseriu o mesmo vínculo (viola a PK composta). Traduz para 409.
                throw new UserConflictException("Conflito ao atualizar os perfis. Tente novamente.");
            }
            catch (DbUpdateConcurrencyException)
            {
                // Corrida de remoção: a linha alvo já havia sido removida
                // concorrentemente (0 linhas afetadas). Traduz para 409.
                throw new UserConflictException("Conflito ao atualizar os perfis. Tente novamente.");
            }
        }

        var list = await BuildAsync(orgId, onlyUserId: id, ct);
        return list.Count == 0 ? null : list[0];
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

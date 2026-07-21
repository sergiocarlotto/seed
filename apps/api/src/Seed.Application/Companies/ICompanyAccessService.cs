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

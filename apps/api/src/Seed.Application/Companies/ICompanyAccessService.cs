namespace Seed.Application.Companies;

// Alvo (usuário ou empresa) fora da organização do chamador, ou empresa fora do
// seu escopo concedível (ADR-0014). → 404, sem vazar existência.
public class CompanyAccessNotFoundException(string message) : Exception(message);

// Conflito de concorrência ao aplicar a mutação (índice único (UserId,
// CompanyId) ou remoção simultânea da mesma linha). → 409.
public class CompanyAccessConflictException(string message) : Exception(message);

// Payload malformado — hoje, lista ausente onde o endpoint DEFINE um conjunto. → 400.
public class CompanyAccessValidationException(string message) : Exception(message);

// Usuário da organização, com a marca de quem já tem acesso à empresa em foco.
public record CompanyUserAccessDto(Guid Id, string FullName, string Email, bool HasAccess);

// Requests (allow-list — organização e ator vêm sempre da sessão).
//
// A lista continua anulável no contrato para que a AUSÊNCIA da chave seja
// detectável e vire 400. Ausente não é o mesmo que `[]`: `[]` é a intenção
// deliberada de esvaziar o conjunto; ausente é bug de cliente, payload truncado
// ou retry parcial — e tratá-lo como `[]` revogaria tudo em silêncio, que é
// justamente como se cria uma empresa órfã por acidente.
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

namespace Seed.Application.Companies;

// Contexto mínimo do usuário para o serviço de empresas: a organização (tenant)
// sob a qual ele opera. A autorização funcional é feita pelo gate de permissão.
public record UserContext(Guid OrganizationId);

namespace Seed.Application.Audit;

// Registra um evento de auditoria na MESMA unidade de trabalho da mutação (não
// chama SaveChanges; o serviço chamador persiste tudo junto, atômico). O ator e
// o horário vêm do contexto; o chamador informa a organização e o alvo.
public interface IAuditLog
{
    void Record(Guid organizationId, string action, string entityType, string entityId, object? metadata = null);
}

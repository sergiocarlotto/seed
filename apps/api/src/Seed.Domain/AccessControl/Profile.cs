using Seed.Domain.Common;

namespace Seed.Domain.AccessControl;

// Perfil configurável, escopo organização. "Arquivar" usa Status (não exclusão
// física); o soft delete de Entity permanece disponível mas não é o mecanismo
// de arquivamento.
public class Profile : Entity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public ProfileStatus Status { get; set; } = ProfileStatus.Active;
}

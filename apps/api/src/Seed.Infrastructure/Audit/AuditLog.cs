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

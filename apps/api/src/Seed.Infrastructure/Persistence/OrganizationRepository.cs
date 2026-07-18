using Microsoft.EntityFrameworkCore;
using Seed.Application.Organizations;
using Seed.Domain.Organizations;

namespace Seed.Infrastructure.Persistence;

public class OrganizationRepository(SeedDbContext db) : IOrganizationRepository
{
    public async Task<List<(Organization Org, OrganizationRole Role)>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var q = from m in db.Memberships
                join o in db.Organizations on m.OrganizationId equals o.Id
                where m.UserId == userId
                select new { o, m.Role };
        return (await q.ToListAsync(ct)).Select(x => (x.o, x.Role)).ToList();
    }

    public Task<Organization?> GetByIdForUserAsync(Guid orgId, Guid userId, CancellationToken ct) =>
        (from o in db.Organizations
         where o.Id == orgId && db.Memberships.Any(m => m.OrganizationId == orgId && m.UserId == userId)
         select o).FirstOrDefaultAsync(ct);

    public async Task<OrganizationRole?> GetRoleAsync(Guid orgId, Guid userId, CancellationToken ct)
    {
        var m = await db.Memberships.FirstOrDefaultAsync(x => x.OrganizationId == orgId && x.UserId == userId, ct);
        return m?.Role;
    }

    public async Task AddAsync(Organization org, OrganizationMembership ownerMembership, CancellationToken ct)
    {
        await db.Organizations.AddAsync(org, ct);
        await db.Memberships.AddAsync(ownerMembership, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

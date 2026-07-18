using Microsoft.EntityFrameworkCore;
using Seed.Application.Companies;
using Seed.Domain.Access;
using Seed.Domain.Companies;

namespace Seed.Infrastructure.Persistence;

public class CompanyRepository(SeedDbContext db) : ICompanyRepository
{
    public async Task<List<Company>> ListForUserAsync(Guid userId, CancellationToken ct) =>
        await (from a in db.UserCompanyAccesses
               join c in db.Companies on a.CompanyId equals c.Id
               where a.UserId == userId
               orderby c.Name
               select c).ToListAsync(ct);

    public Task<Company?> GetForUserAsync(Guid companyId, Guid userId, CancellationToken ct) =>
        (from c in db.Companies
         where c.Id == companyId
            && db.UserCompanyAccesses.Any(a => a.CompanyId == companyId && a.UserId == userId)
         select c).FirstOrDefaultAsync(ct);

    public async Task<UserContext?> GetUserContextAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        return u is null ? null : new UserContext(u.OrganizationId, u.OrgRole);
    }

    public async Task AddAsync(Company company, UserCompanyAccess access, CancellationToken ct)
    {
        await db.Companies.AddAsync(company, ct);
        await db.UserCompanyAccesses.AddAsync(access, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

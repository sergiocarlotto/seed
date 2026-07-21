using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Seed.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Seed.IntegrationTests;

// Data migration da ADR-0012 (dentro de AddAccessControl): converte o estado da
// ADR-0010 (orgRole Admin/Member) para o modelo de perfis.
//
// Postura adotada (diverge da letra da spec por decisão de segurança): TODOS os
// ex-admins ficam vinculados ao perfil "Administrador" (vínculo revogável pela
// aplicação) e apenas UM por organização vira is_owner — o owner tem bypass
// total e é irrevogável pela app, então não se distribui esse privilégio.
//
// O teste aplica as migrations passo a passo: para na InitialCreate, injeta o
// estado legado por SQL cru e então aplica a AddAccessControl.
public class AccessControlDataMigrationTests : IAsyncLifetime
{
    private const string InitialCreate = "20260718121444_InitialCreate";
    private const string AddAccessControl = "20260719185719_AddAccessControl";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private SeedDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SeedDbContext>().UseNpgsql(_db.GetConnectionString()).Options);

    // Insere uma organização e seus usuários no schema legado (com OrgRole).
    // orgRole: 0 = Admin, 1 = Member.
    private static async Task SeedLegacyOrgAsync(SeedDbContext db, Guid orgId, string name, params (Guid Id, int OrgRole)[] users)
    {
        await db.Database.ExecuteSqlRawAsync(
            $"""
            INSERT INTO "Organizations" ("Id", "Name", "Status", "CreatedAt", "UpdatedAt")
            VALUES ('{orgId}', '{name}', 0, now(), now());
            """);

        foreach (var (id, orgRole) in users)
            await db.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO "AspNetUsers"
                    ("Id", "FullName", "OrganizationId", "OrgRole", "UserName", "NormalizedUserName",
                     "Email", "NormalizedEmail", "EmailConfirmed", "PhoneNumberConfirmed",
                     "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                VALUES ('{id}', 'Usuário {id}', '{orgId}', {orgRole}, '{id}@x.local', '{id}@X.LOCAL',
                        '{id}@x.local', '{id}@X.LOCAL', TRUE, FALSE, FALSE, TRUE, 0);
                """);
    }

    private static Task<int> CountAsync(SeedDbContext db, string sql) =>
        db.Database.SqlQueryRaw<int>(sql).SingleAsync();

    [Fact]
    public async Task Migra_admins_para_perfil_e_elege_um_unico_owner_por_organizacao()
    {
        // Ids fixos e ordenados: o desempate do owner é por Id, então o esperado
        // é determinístico (o menor Id de cada organização).
        var orgA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var adminA1 = Guid.Parse("11111111-0000-0000-0000-000000000001");
        var adminA2 = Guid.Parse("22222222-0000-0000-0000-000000000002");
        var adminA3 = Guid.Parse("33333333-0000-0000-0000-000000000003");
        var memberA = Guid.Parse("44444444-0000-0000-0000-000000000004");

        var orgB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        var adminB = Guid.Parse("55555555-0000-0000-0000-000000000005");

        // Organização sem nenhum usuário: a migration não pode quebrar.
        var orgC = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

        await using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(InitialCreate);

            await SeedLegacyOrgAsync(db, orgA, "Org A",
                (adminA1, 0), (adminA2, 0), (adminA3, 0), (memberA, 1));
            await SeedLegacyOrgAsync(db, orgB, "Org B", (adminB, 0));
            await SeedLegacyOrgAsync(db, orgC, "Org C");

            await db.GetService<IMigrator>().MigrateAsync(AddAccessControl);
        }

        await using (var db = CreateContext())
        {
            // Cada organização ganha exatamente um perfil de sistema "Administrador".
            Assert.Equal(3, await CountAsync(db,
                """SELECT count(*)::int AS "Value" FROM "Profiles" WHERE "IsSystem" AND "Name" = 'Administrador' """));

            // Org A: um único owner, e é o de menor Id.
            Assert.Equal(1, await CountAsync(db,
                $"""SELECT count(*)::int AS "Value" FROM "AspNetUsers" WHERE "OrganizationId" = '{orgA}' AND "IsOwner" """));
            Assert.Equal(1, await CountAsync(db,
                $"""SELECT count(*)::int AS "Value" FROM "AspNetUsers" WHERE "Id" = '{adminA1}' AND "IsOwner" """));

            // Os três ex-admins de A mantêm acesso via perfil (revogável pela app).
            Assert.Equal(3, await CountAsync(db,
                $"""
                SELECT count(*)::int AS "Value" FROM "UserProfiles" up
                JOIN "Profiles" p ON p."Id" = up."ProfileId"
                WHERE p."OrganizationId" = '{orgA}'
                """));

            // O member migra sem perfil (consequência assumida na spec).
            Assert.Equal(0, await CountAsync(db,
                $"""SELECT count(*)::int AS "Value" FROM "UserProfiles" WHERE "UserId" = '{memberA}' """));

            // Org B: o único admin vira owner e também ganha o vínculo.
            Assert.Equal(1, await CountAsync(db,
                $"""SELECT count(*)::int AS "Value" FROM "AspNetUsers" WHERE "Id" = '{adminB}' AND "IsOwner" """));
            Assert.Equal(1, await CountAsync(db,
                $"""SELECT count(*)::int AS "Value" FROM "UserProfiles" WHERE "UserId" = '{adminB}' """));

            // Nenhum owner a mais no total (org sem usuários não inventa owner).
            Assert.Equal(2, await CountAsync(db,
                """SELECT count(*)::int AS "Value" FROM "AspNetUsers" WHERE "IsOwner" """));
        }
    }

    public async Task InitializeAsync() => await _db.StartAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();
}

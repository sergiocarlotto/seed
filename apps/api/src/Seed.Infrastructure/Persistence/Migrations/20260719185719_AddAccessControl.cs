using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Seed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfilePermissions",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(100)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePermissions", x => new { x.ProfileId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_ProfilePermissions_Permissions_PermissionKey",
                        column: x => x.PermissionKey,
                        principalTable: "Permissions",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProfilePermissions_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => new { x.UserId, x.ProfileId });
                    table.ForeignKey(
                        name: "FK_UserProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePermissions_PermissionKey",
                table: "ProfilePermissions",
                column: "PermissionKey");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_OrganizationId_Name",
                table: "Profiles",
                columns: new[] { "OrganizationId", "Name" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_ProfileId",
                table: "UserProfiles",
                column: "ProfileId");

            // Data migration da ADR-0012: converte o estado da ADR-0010 (OrgRole
            // Admin=0 / Member=1) para o modelo de perfis. Em banco novo é no-op
            // (o DataSeeder roda depois e já cria o owner).
            //
            // O perfil nasce SEM permissões: quem as concede é o
            // AccessControlBootstrapper no boot (top-up), pois o catálogo de
            // Permission só é populado pelo reconciliador, após as migrations.
            migrationBuilder.Sql(
                """
                INSERT INTO "Profiles"
                    ("Id", "OrganizationId", "Name", "Description", "IsSystem", "Status", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), o."Id", 'Administrador',
                       'Perfil de sistema com todas as permissões.', TRUE, 0, now(), now()
                FROM "Organizations" o;
                """);

            // Todos os ex-admins mantêm o acesso administrativo por VÍNCULO de
            // perfil — que a aplicação consegue revogar depois.
            migrationBuilder.Sql(
                """
                INSERT INTO "UserProfiles" ("UserId", "ProfileId")
                SELECT u."Id", p."Id"
                FROM "AspNetUsers" u
                JOIN "Profiles" p
                  ON p."OrganizationId" = u."OrganizationId" AND p."IsSystem"
                WHERE u."OrgRole" = 0;
                """);

            // Apenas UM admin por organização vira owner (desempate determinístico
            // pelo menor Id). O owner tem bypass total e é irrevogável pela app,
            // então esse privilégio não é distribuído a todos os ex-admins.
            // Membros (OrgRole=1) migram sem perfil — consequência assumida no design.
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers" SET "IsOwner" = TRUE
                WHERE "Id" IN (
                    SELECT DISTINCT ON ("OrganizationId") "Id"
                    FROM "AspNetUsers"
                    WHERE "OrgRole" = 0
                    ORDER BY "OrganizationId", "Id"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfilePermissions");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropColumn(
                name: "IsOwner",
                table: "AspNetUsers");
        }
    }
}

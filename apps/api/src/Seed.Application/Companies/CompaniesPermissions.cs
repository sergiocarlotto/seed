using Seed.Application.AccessControl;

namespace Seed.Application.Companies;

// Permissões da funcionalidade de empresas, declaradas pelo módulo organizations
// (ADR-0010). Chaves estáveis e imutáveis. companies.access = ver/acessar;
// companies.manage = criar/editar/excluir. O campo Module é o AGRUPADOR de UI do
// catálogo (como access_control agrupa profiles.* e users.*), por isso é
// "organizations", o módulo dono — distinto do prefixo da chave. A visibilidade
// continua também condicionada ao eixo de empresa (UserCompanyAccess).
public static class CompaniesPermissions
{
    public const string Module = "organizations";

    public const string Access = "companies.access";
    public const string Manage = "companies.manage";
    public const string GrantAccess = "companies.grant_access";

    public static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        new(Access, Module, "Acessar empresas",
            "Ver e acessar a funcionalidade de empresas."),
        new(Manage, Module, "Gerir empresas",
            "Criar, editar e excluir empresas."),
        new(GrantAccess, Module, "Conceder acesso a empresas",
            "Conceder e revogar o acesso de usuários às empresas."),
    ];
}

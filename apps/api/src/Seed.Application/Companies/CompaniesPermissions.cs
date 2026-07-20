using Seed.Application.AccessControl;

namespace Seed.Application.Companies;

// Permissões da funcionalidade de empresas (parte do módulo organizations, ADR-0010).
// Chaves estáveis e imutáveis. companies.access = ver/acessar; companies.manage =
// criar/editar/excluir. A visibilidade continua também condicionada ao eixo de
// empresa (UserCompanyAccess) — as duas travas são avaliadas juntas.
public static class CompaniesPermissions
{
    public const string Module = "companies";

    public const string Access = "companies.access";
    public const string Manage = "companies.manage";

    public static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        new(Access, Module, "Acessar empresas",
            "Ver e acessar a funcionalidade de empresas."),
        new(Manage, Module, "Gerir empresas",
            "Criar, editar e excluir empresas."),
    ];
}

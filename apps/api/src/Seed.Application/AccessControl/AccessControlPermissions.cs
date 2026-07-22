namespace Seed.Application.AccessControl;

// Permissões declaradas pelo módulo AccessControl. Chaves estáveis e imutáveis
// (renomear = obsoletar a antiga e criar nova).
public static class AccessControlPermissions
{
    public const string Module = "access_control";

    public const string ProfilesManage = "profiles.manage";
    public const string ProfilesAssign = "profiles.assign";
    public const string UsersManage = "users.manage";

    public static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        new(ProfilesManage, Module, "Gerir perfis",
            "Criar, editar e arquivar perfis e definir suas permissões."),
        new(ProfilesAssign, Module, "Atribuir perfis",
            "Atribuir e remover perfis dos usuários."),
        new(UsersManage, Module, "Gerir usuários",
            "Criar, listar, ativar e desativar usuários."),
    ];
}

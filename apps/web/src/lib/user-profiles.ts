/**
 * Regra de UX (espelho do backend) para habilitar a atribuição/remoção de um
 * perfil a um usuário. O backend é a barreira real; isto só habilita/desabilita
 * o checkbox. Reflete: owner do alvo é somente leitura; postura B (perfil
 * `is_system` só o owner atribui); exige `profiles.assign`.
 */
export function canAssignProfile(args: {
  canAssign: boolean;
  targetIsOwner: boolean;
  meIsOwner: boolean;
  profileIsSystem: boolean;
}): boolean {
  const { canAssign, targetIsOwner, meIsOwner, profileIsSystem } = args;
  if (!canAssign) return false;
  if (targetIsOwner) return false;
  if (profileIsSystem) return meIsOwner;
  return true;
}

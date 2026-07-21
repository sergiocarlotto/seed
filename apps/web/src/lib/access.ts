// Sujeito mínimo para a checagem de acesso de UX. Estrutural de propósito, para
// não acoplar a `Me` (evita import circular) e facilitar o teste.
export type AccessSubject = { isOwner: boolean; permissions: string[] };

/**
 * Espelho de UX da autorização do backend: o owner tem bypass funcional
 * completo; os demais precisam ter a permissão na lista efetiva. NUNCA é a
 * barreira real — o backend é. Serve só para esconder menus/ações.
 */
export function can(subject: AccessSubject, key: string): boolean {
  return subject.isOwner || subject.permissions.includes(key);
}

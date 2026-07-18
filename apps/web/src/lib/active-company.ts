import type { Company } from "./types";

export const ACTIVE_COMPANY_COOKIE = "active-company";

export type ActiveCompanyResolution = {
  /** A empresa ativa efetiva, ou null se o usuário não tem empresas. */
  active: Company | null;
  /** true quando o cookie precisa ser (re)escrito ou limpo pelo servidor. */
  corrected: boolean;
};

/**
 * Regra dura: a empresa ativa é SEMPRE uma empresa que o usuário acessa.
 * - cookie válido  -> usa o cookie.
 * - inválido/ausente -> primeira empresa por nome, e sinaliza `corrected`.
 * - sem empresas   -> null, e sinaliza `corrected` para limpar cookie obsoleto.
 */
export function resolveActiveCompany(
  companies: Company[],
  cookieId: string | undefined
): ActiveCompanyResolution {
  const sorted = [...companies].sort((a, b) => a.name.localeCompare(b.name));

  if (sorted.length === 0) {
    return { active: null, corrected: cookieId != null };
  }

  const fromCookie = cookieId ? sorted.find((c) => c.id === cookieId) : undefined;
  if (fromCookie) {
    return { active: fromCookie, corrected: false };
  }

  return { active: sorted[0], corrected: true };
}

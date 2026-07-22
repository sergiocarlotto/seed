import type { AccessSubject } from "./access";
import { can } from "./access";

/**
 * Espelho de UX de `companies.grant_access`. O backend é a barreira real; isto
 * só decide se o checklist aparece editável.
 */
export function canGrantCompanies(subject: AccessSubject): boolean {
  return can(subject, "companies.grant_access");
}

/**
 * Monta o payload de `PUT /users/{id}/companies` a partir do que o operador
 * marcou. Só entram ids do escopo concedível dele: o que está fora nunca é
 * enviado, e o backend preserva essas concessões em vez de removê-las por
 * ausência (ADR-0014, regra 2). Sem esse filtro, um operador que não enxerga a
 * empresa X removeria X sem querer a cada gravação.
 */
export function mergePreservingOutOfScope(args: {
  selected: string[];
  scope: string[];
}): string[] {
  const selected = new Set(args.selected);
  return args.scope.filter((id) => selected.has(id));
}

import type { PermissionGroup } from "./types";

export type ModuleCheckState = "checked" | "indeterminate" | "unchecked";

/** Estado do checkbox de cabeçalho de um módulo dado o conjunto selecionado. */
export function moduleState(group: PermissionGroup, selected: ReadonlySet<string>): ModuleCheckState {
  const keys = group.permissions.map((p) => p.key);
  const count = keys.filter((k) => selected.has(k)).length;
  if (count === 0) return "unchecked";
  if (count === keys.length) return "checked";
  return "indeterminate";
}

/** Alterna o módulo inteiro: se todas marcadas, desmarca; senão marca todas. Não muta a entrada. */
export function toggleModule(group: PermissionGroup, selected: ReadonlySet<string>): Set<string> {
  const keys = group.permissions.map((p) => p.key);
  const next = new Set(selected);
  const allOn = keys.every((k) => next.has(k));
  for (const k of keys) {
    if (allOn) next.delete(k);
    else next.add(k);
  }
  return next;
}

/** Alterna uma única permissão. Não muta a entrada. */
export function togglePermission(key: string, selected: ReadonlySet<string>): Set<string> {
  const next = new Set(selected);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  return next;
}

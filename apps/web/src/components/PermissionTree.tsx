"use client";

import { useState } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { Checkbox } from "@/components/ui/checkbox";
import { moduleState, toggleModule, togglePermission } from "@/lib/permission-tree";
import type { PermissionGroup } from "@/lib/types";

type PermissionTreeProps = {
  groups: PermissionGroup[];
  selected: Set<string>;
  onChange: (next: Set<string>) => void;
  disabled?: boolean;
};

/**
 * Seletor de permissões agrupado por `module` (acordeão). O checkbox de cabeçalho
 * marca/desmarca o módulo inteiro e mostra estado indeterminado quando parcial.
 * Abre expandido; empilha naturalmente no mobile. `disabled` deixa tudo em leitura
 * (perfil de sistema).
 */
export function PermissionTree({ groups, selected, onChange, disabled = false }: PermissionTreeProps) {
  const [open, setOpen] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(groups.map((g) => [g.module, true]))
  );

  if (groups.length === 0) {
    return <p className="text-sm text-muted-foreground">Nenhuma permissão disponível.</p>;
  }

  return (
    <div className="flex flex-col gap-3" data-testid="permission-tree">
      {groups.map((group) => {
        const state = moduleState(group, selected);
        const isOpen = open[group.module] ?? true;
        return (
          <div key={group.module} className="overflow-hidden rounded-lg ring-1 ring-foreground/10">
            <div className="flex items-center gap-2 bg-muted/50 px-3 py-2">
              <Checkbox
                checked={state === "checked"}
                indeterminate={state === "indeterminate"}
                disabled={disabled}
                onCheckedChange={() => onChange(toggleModule(group, selected))}
                aria-label={`Selecionar todas de ${group.module}`}
              />
              <span className="font-medium capitalize">{group.module}</span>
              <button
                type="button"
                onClick={() => setOpen((o) => ({ ...o, [group.module]: !isOpen }))}
                aria-label={isOpen ? "Recolher" : "Expandir"}
                aria-expanded={isOpen}
                className="ml-auto rounded p-1 text-muted-foreground hover:bg-muted"
              >
                <ChevronDown className={cn("size-4 transition-transform", !isOpen && "-rotate-90")} />
              </button>
            </div>
            {isOpen && (
              <ul className="flex flex-col">
                {group.permissions.map((perm) => (
                  <li key={perm.key} className="flex items-center gap-2 px-3 py-2 pl-8">
                    <Checkbox
                      checked={selected.has(perm.key)}
                      disabled={disabled}
                      onCheckedChange={() => onChange(togglePermission(perm.key, selected))}
                      aria-label={perm.displayName}
                    />
                    <span className="text-sm">{perm.displayName}</span>
                    <span className="text-xs text-muted-foreground">· {perm.key}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        );
      })}
    </div>
  );
}

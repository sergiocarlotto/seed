"use client";

import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { canGrantCompanies, mergePreservingOutOfScope } from "@/lib/company-access";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import type { Company, EntityRef } from "@/lib/types";

type UserCompaniesFormProps = {
  userId: string;
  currentCompanies: EntityRef[];
  /** Escopo concedível do operador: o que ele pode conceder ou revogar. */
  grantableCompanies: Company[] | null;
};

/**
 * Concessão de acesso a empresas do usuário. Editável só com
 * `companies.grant_access` e quando o escopo pôde ser carregado. Ao contrário de
 * status e perfis, o owner **é** alvo válido aqui: ele está sujeito ao eixo de
 * empresa (ADR-0012) e precisa poder receber acesso.
 *
 * Só se envia o que está no escopo do operador; empresas que ele não enxerga
 * ficam intactas no backend (ADR-0014).
 */
export function UserCompaniesForm({ userId, currentCompanies, grantableCompanies }: UserCompaniesFormProps) {
  const me = useSession();
  const editable = canGrantCompanies(me) && grantableCompanies !== null;
  const [selected, setSelected] = useState<Set<string>>(new Set(currentCompanies.map((c) => c.id)));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  if (!editable) {
    return currentCompanies.length === 0 ? (
      <p className="text-sm text-muted-foreground">Nenhuma empresa.</p>
    ) : (
      <div className="flex flex-wrap gap-1.5">
        {currentCompanies.map((c) => (
          <Badge key={c.id}>{c.name}</Badge>
        ))}
      </div>
    );
  }

  const scope = grantableCompanies.map((c) => c.id);
  // Empresas do usuário fora do escopo do operador: mostradas em leitura, para
  // ele não achar que "desmarcou" algo que nunca esteve sob seu alcance.
  const outOfScope = currentCompanies.filter((c) => !scope.includes(c.id));

  function toggle(id: string) {
    setSaved(false);
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function handleSave() {
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      await api.put(`/users/${userId}/companies`, {
        companyIds: mergePreservingOutOfScope({ selected: [...selected], scope }),
      });
      setSaved(true);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {grantableCompanies.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Você não tem acesso a nenhuma empresa, então não há o que conceder.
        </p>
      ) : (
        <ul className="flex flex-col gap-1.5" data-testid="user-companies">
          {grantableCompanies.map((c) => (
            <li key={c.id} className="flex items-center gap-2">
              <Checkbox
                checked={selected.has(c.id)}
                onCheckedChange={() => toggle(c.id)}
                aria-label={c.name}
              />
              <span className="text-sm">{c.name}</span>
            </li>
          ))}
        </ul>
      )}

      {outOfScope.length > 0 && (
        <div className="flex flex-col gap-1.5">
          <span className="text-xs text-muted-foreground">
            Fora do seu acesso (mantidas como estão):
          </span>
          <div className="flex flex-wrap gap-1.5">
            {outOfScope.map((c) => (
              <Badge key={c.id}>{c.name}</Badge>
            ))}
          </div>
        </div>
      )}

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {saved && <p className="text-sm text-emerald-600 dark:text-emerald-400">Empresas atualizadas.</p>}

      {grantableCompanies.length > 0 && (
        <div>
          <Button onClick={handleSave} disabled={saving} data-testid="save-companies">
            {saving ? "Salvando..." : "Salvar empresas"}
          </Button>
        </div>
      )}
    </div>
  );
}

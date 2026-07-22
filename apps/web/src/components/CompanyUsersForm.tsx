"use client";

import { useEffect, useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { canGrantCompanies } from "@/lib/company-access";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Loading } from "@/components/states";
import type { CompanyUserAccess } from "@/lib/types";

/**
 * Quem tem acesso a esta empresa. Aqui o conjunto é completo: a empresa já está
 * no escopo do operador (senão o backend responde 404), então todos os usuários
 * da organização são alvo legítimo.
 */
export function CompanyUsersForm({ companyId }: { companyId: string }) {
  const me = useSession();
  const allowed = canGrantCompanies(me);
  const [users, setUsers] = useState<CompanyUserAccess[] | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    // Sem permissão o componente nem chega a renderizar o estado de loading
    // (early return abaixo), então não há setState a fazer aqui.
    if (!allowed) return;
    let active = true;
    (async () => {
      try {
        const data = await api.get<CompanyUserAccess[]>(`/companies/${companyId}/users`);
        if (!active) return;
        setUsers(data);
        setSelected(new Set(data.filter((u) => u.hasAccess).map((u) => u.id)));
      } catch (err) {
        if (active) setError(errorMessage(err));
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [companyId, allowed]);

  if (!allowed) return null;
  if (loading) return <Loading rows={2} />;

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
      await api.put(`/companies/${companyId}/users`, { userIds: [...selected] });
      setSaved(true);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {users === null || users.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nenhum usuário na organização.</p>
      ) : (
        <ul className="flex flex-col gap-1.5" data-testid="company-users">
          {users.map((u) => (
            <li key={u.id} className="flex items-center gap-2">
              <Checkbox
                checked={selected.has(u.id)}
                onCheckedChange={() => toggle(u.id)}
                aria-label={u.fullName}
              />
              <span className="flex min-w-0 flex-col sm:flex-row sm:gap-2">
                <span className="truncate text-sm">{u.fullName}</span>
                <span className="truncate text-xs text-muted-foreground">{u.email}</span>
              </span>
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {saved && <p className="text-sm text-emerald-600 dark:text-emerald-400">Acessos atualizados.</p>}

      {users !== null && users.length > 0 && (
        <div>
          <Button onClick={handleSave} disabled={saving} data-testid="save-company-users">
            {saving ? "Salvando..." : "Salvar acessos"}
          </Button>
        </div>
      )}
    </div>
  );
}

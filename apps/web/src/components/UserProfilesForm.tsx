"use client";

import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { can } from "@/lib/access";
import { canAssignProfile } from "@/lib/user-profiles";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import type { EntityRef, ProfileSummary } from "@/lib/types";

type UserProfilesFormProps = {
  userId: string;
  targetIsOwner: boolean;
  currentProfiles: EntityRef[];
  allProfiles: ProfileSummary[] | null;
};

/**
 * Atribuição de perfis do usuário. Editável só quando o operador tem
 * `profiles.assign`, o catálogo de perfis pôde ser carregado e o alvo não é o
 * owner; caso contrário, mostra os perfis atuais em leitura. `is_system` só o
 * owner marca (postura B, via `canAssignProfile`).
 */
export function UserProfilesForm({ userId, targetIsOwner, currentProfiles, allProfiles }: UserProfilesFormProps) {
  const me = useSession();
  const canAssign = can(me, "profiles.assign");
  const editable = canAssign && !targetIsOwner && allProfiles !== null;
  const [selected, setSelected] = useState<Set<string>>(new Set(currentProfiles.map((p) => p.id)));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  if (!editable) {
    return currentProfiles.length === 0 ? (
      <p className="text-sm text-muted-foreground">Sem perfil.</p>
    ) : (
      <div className="flex flex-wrap gap-1.5">
        {currentProfiles.map((p) => (
          <Badge key={p.id}>{p.name}</Badge>
        ))}
      </div>
    );
  }

  const active = allProfiles.filter((p) => p.status.toLowerCase() === "active");

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
      await api.put(`/users/${userId}/profiles`, { profileIds: [...selected] });
      setSaved(true);
    } catch (err) {
      // 409: o usuário mudou (corrida) — a mensagem pede recarregar.
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <ul className="flex flex-col gap-1.5" data-testid="user-profiles">
        {active.map((p) => {
          const enabled = canAssignProfile({
            canAssign,
            targetIsOwner,
            meIsOwner: me.isOwner,
            profileIsSystem: p.isSystem,
          });
          return (
            <li key={p.id} className="flex items-center gap-2">
              <Checkbox
                checked={selected.has(p.id)}
                disabled={!enabled}
                onCheckedChange={() => toggle(p.id)}
                aria-label={p.name}
              />
              <span className="text-sm">{p.name}</span>
              {p.isSystem && <Badge variant="system">Sistema</Badge>}
            </li>
          );
        })}
      </ul>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error} — recarregue a página e tente novamente.
        </p>
      )}
      {saved && (
        <p className="text-sm text-emerald-600 dark:text-emerald-400">Perfis atualizados.</p>
      )}
      <div>
        <Button onClick={handleSave} disabled={saving} data-testid="save-profiles">
          {saving ? "Salvando..." : "Salvar perfis"}
        </Button>
      </div>
    </div>
  );
}

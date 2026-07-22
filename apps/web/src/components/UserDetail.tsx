"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { can } from "@/lib/access";
import { useSetPageHeader } from "@/lib/page-header";
import { UserProfilesForm } from "@/components/UserProfilesForm";
import { UserCompaniesForm } from "@/components/UserCompaniesForm";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { UserRow, ProfileSummary, Company } from "@/lib/types";

/**
 * Detalhe do usuário: status (switch), perfis (checklist) e empresas
 * (checklist). Owner é somente leitura para status e perfis; empresas
 * seguem o eixo de acesso (ADR-0012) e permanecem editáveis mesmo para o
 * owner. O switch exige `users.manage`; a edição de perfis é governada pelo
 * `UserProfilesForm`, e a de empresas pelo `UserCompaniesForm`.
 */
export function UserDetail({
  user,
  allProfiles,
  grantableCompanies,
}: {
  user: UserRow;
  allProfiles: ProfileSummary[] | null;
  grantableCompanies: Company[] | null;
}) {
  useSetPageHeader({ title: user.fullName, breadcrumb: ["Administração", "Usuários", user.fullName] });
  const me = useSession();
  const router = useRouter();
  const canManage = can(me, "users.manage") && !user.isOwner;
  const [active, setActive] = useState(user.status.toLowerCase() === "active");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleToggle(next: boolean) {
    setSaving(true);
    setError(null);
    setActive(next);
    try {
      await api.patch(`/users/${user.id}/status`, { active: next });
      router.refresh();
    } catch (err) {
      setActive(!next); // desfaz o otimismo em caso de falha
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <div className="flex items-center justify-end">
        <Button variant="ghost" size="sm" render={<Link href="/users" />}>
          Voltar
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            {user.fullName}
            {user.isOwner && <Badge variant="info">Owner</Badge>}
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">{user.email}</p>

          <div className="flex items-center justify-between">
            <div className="flex flex-col gap-1">
              <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                Status
              </span>
              {active ? <Badge variant="success">Ativo</Badge> : <Badge>Inativo</Badge>}
            </div>
            <Switch
              checked={active}
              disabled={!canManage || saving}
              onCheckedChange={handleToggle}
              aria-label="Ativar ou desativar usuário"
              data-testid="user-status"
            />
          </div>
          {user.isOwner && (
            <p className="text-xs text-muted-foreground">
              O dono da organização é somente leitura: não pode ser desativado nem ter perfis
              alterados pela aplicação.
            </p>
          )}
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Perfis</CardTitle>
        </CardHeader>
        <CardContent>
          <UserProfilesForm
            userId={user.id}
            targetIsOwner={user.isOwner}
            currentProfiles={user.profiles}
            allProfiles={allProfiles}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Empresas acessíveis</CardTitle>
        </CardHeader>
        <CardContent>
          <UserCompaniesForm
            userId={user.id}
            currentCompanies={user.companies}
            grantableCompanies={grantableCompanies}
          />
        </CardContent>
      </Card>
    </div>
  );
}

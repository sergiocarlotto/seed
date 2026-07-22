"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { profileSchema, firstError } from "@/lib/form-schemas";
import { useSetPageHeader } from "@/lib/page-header";
import { PermissionTree } from "@/components/PermissionTree";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import type { PermissionGroup, ProfileDetail } from "@/lib/types";

type ProfileFormProps =
  | { mode: "create"; groups: PermissionGroup[]; profile?: undefined }
  | { mode: "edit"; groups: PermissionGroup[]; profile: ProfileDetail };

/**
 * Editor de perfil (página cheia). No modo edit de um perfil `is_system`, tudo
 * fica em leitura (nome, descrição e seletor desabilitados) — a app não altera o
 * perfil "Administrador".
 */
export function ProfileForm({ mode, groups, profile }: ProfileFormProps) {
  const readOnly = mode === "edit" && profile.isSystem;
  useSetPageHeader({
    title: mode === "create" ? "Novo perfil" : readOnly ? "Perfil (somente leitura)" : "Editar perfil",
    breadcrumb: ["Administração", "Perfis", mode === "create" ? "Novo" : "Editar"],
  });
  const router = useRouter();
  const [name, setName] = useState(profile?.name ?? "");
  const [description, setDescription] = useState(profile?.description ?? "");
  const [selected, setSelected] = useState<Set<string>>(new Set(profile?.permissionKeys ?? []));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    const parsed = profileSchema.safeParse({ name, description });
    if (!parsed.success) {
      setError(firstError(parsed.error));
      return;
    }

    setSaving(true);
    const body = { ...parsed.data, permissionKeys: [...selected] };
    try {
      if (mode === "create") await api.post("/profiles", body);
      else await api.put(`/profiles/${profile.id}`, body);
      router.push("/profiles");
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
      setSaving(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <div className="flex items-center justify-between gap-4">
        {readOnly && <Badge variant="system">Sistema</Badge>}
        <Button variant="ghost" size="sm" className="ml-auto" render={<Link href="/profiles" />}>
          Voltar
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="flex flex-col gap-5">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="name">Nome do perfil</Label>
          <Input
            id="name"
            required
            value={name}
            disabled={readOnly}
            onChange={(e) => setName(e.target.value)}
            data-testid="profile-name"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="description">Descrição</Label>
          <Input
            id="description"
            value={description}
            disabled={readOnly}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label>Permissões</Label>
          <PermissionTree groups={groups} selected={selected} onChange={setSelected} disabled={readOnly} />
        </div>

        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}

        {!readOnly && (
          <div>
            <Button type="submit" size="lg" disabled={saving} data-testid="profile-submit">
              {saving ? "Salvando..." : mode === "create" ? "Criar perfil" : "Salvar"}
            </Button>
          </div>
        )}
      </form>
    </div>
  );
}

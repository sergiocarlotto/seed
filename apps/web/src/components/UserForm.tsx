"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSetPageHeader } from "@/lib/page-header";
import { userSchema, firstError } from "@/lib/form-schemas";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { UserRow } from "@/lib/types";

/**
 * Criação de usuário. O administrador define a senha inicial — não há convite
 * por e-mail (fora do escopo até existir e-mail transacional). A confirmação é
 * regra de tela e não vai para a API; a política de senha de verdade é do
 * Identity, no backend, que devolve a mensagem. Ao salvar, vai para o detalhe,
 * onde perfis e empresas são configurados.
 */
export function UserForm() {
  useSetPageHeader({ title: "Novo usuário", breadcrumb: ["Administração", "Usuários", "Novo"] });
  const router = useRouter();
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    // O schema apara os campos e confere a confirmação (ADR-0002).
    const parsed = userSchema.safeParse({ fullName, email, password, confirm });
    if (!parsed.success) {
      setError(firstError(parsed.error));
      return;
    }

    setSaving(true);
    try {
      // `confirm` não vai para a API: é regra de tela, não campo do contrato.
      const created = await api.post<UserRow>("/users", {
        fullName: parsed.data.fullName,
        email: parsed.data.email,
        password: parsed.data.password,
      });
      router.push(`/users/${created.id}`);
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
      setSaving(false);
    }
  }

  return (
    // max-w-sm já é uma coluna em qualquer largura: atende a convenção mobile.
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dados do usuário</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="fullName">Nome completo</Label>
              <Input
                id="fullName"
                type="text"
                required
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                required
                autoComplete="off"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="password">Senha inicial</Label>
              <Input
                id="password"
                type="password"
                required
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Mínimo de 8 caracteres, com maiúscula, minúscula, número e símbolo. Combine com a
                pessoa um canal seguro para transmitir esta senha.
              </p>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="confirm">Confirmar senha</Label>
              <Input
                id="confirm"
                type="password"
                required
                autoComplete="new-password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
              />
            </div>

            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}

            <Button type="submit" size="lg" disabled={saving} data-testid="save-user">
              {saving ? "Criando..." : "Criar usuário"}
            </Button>

            <p className="text-xs text-muted-foreground">
              O usuário nasce ativo, sem perfis e sem empresas — ou seja, sem nenhum acesso. Configure
              perfis e empresas na tela seguinte.
            </p>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

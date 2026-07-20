"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSetPageHeader } from "@/lib/page-header";
import { EmptyState } from "@/components/states";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { ProfileSummary } from "@/lib/types";

/**
 * Lista de perfis. Perfil `isSystem` só tem "Ver" (leitura); os demais têm
 * "Editar" e "Arquivar". Arquivar chama DELETE /profiles/{id} (soft) e avisa
 * quantos usuários serão afetados.
 */
export function ProfilesList({ initial }: { initial: ProfileSummary[] }) {
  useSetPageHeader({ title: "Perfis", breadcrumb: ["Administração", "Perfis"] });
  const router = useRouter();
  const [profiles, setProfiles] = useState<ProfileSummary[]>(initial);
  const [error, setError] = useState<string | null>(null);
  const [target, setTarget] = useState<ProfileSummary | null>(null);
  const [archiving, setArchiving] = useState(false);

  async function handleArchive() {
    if (!target) return;
    setArchiving(true);
    setError(null);
    try {
      await api.del<void>(`/profiles/${target.id}`);
      setProfiles((prev) => prev.filter((p) => p.id !== target.id));
      setTarget(null);
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setArchiving(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      {profiles.length > 0 && (
        <div>
          <Button render={<Link href="/profiles/new" />} data-testid="new-profile">
            Novo perfil
          </Button>
        </div>
      )}

      {profiles.length === 0 ? (
        <EmptyState
          title="Nenhum perfil ainda"
          description="Crie o primeiro perfil para conceder permissões aos usuários."
          action={
            <Button render={<Link href="/profiles/new" />} data-testid="new-profile">
              Novo perfil
            </Button>
          }
        />
      ) : (
        <div className="rounded-xl ring-1 ring-foreground/10">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead>Descrição</TableHead>
                <TableHead>Usuários</TableHead>
                <TableHead>Tipo</TableHead>
                <TableHead className="text-right">Ações</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {profiles.map((p) => (
                <TableRow key={p.id} data-testid={`profile-row-${p.id}`}>
                  <TableCell className="font-medium">{p.name}</TableCell>
                  <TableCell className="text-muted-foreground">{p.description}</TableCell>
                  <TableCell>{p.userCount}</TableCell>
                  <TableCell>
                    {p.isSystem ? <Badge variant="system">Sistema</Badge> : <Badge>Custom</Badge>}
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-2">
                      {p.isSystem ? (
                        <Button variant="outline" size="sm" render={<Link href={`/profiles/${p.id}`} />}>
                          Ver
                        </Button>
                      ) : (
                        <>
                          <Button variant="outline" size="sm" render={<Link href={`/profiles/${p.id}`} />}>
                            Editar
                          </Button>
                          <Button variant="destructive" size="sm" onClick={() => setTarget(p)}>
                            Arquivar
                          </Button>
                        </>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog
        open={target !== null}
        onOpenChange={(o) => {
          if (!o) {
            setTarget(null);
            setError(null);
          }
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Arquivar perfil</DialogTitle>
            <DialogDescription>
              Arquivar <strong>{target?.name}</strong>? Ele deixará de conceder permissões
              {target && target.userCount > 0 ? (
                <>
                  {" "}
                  para <strong>{target.userCount}</strong> usuário(s) vinculado(s)
                </>
              ) : null}
              . O vínculo é mantido e a ação é reversível ao reativar.
            </DialogDescription>
          </DialogHeader>
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
          <DialogFooter>
            <DialogClose render={<Button variant="outline" disabled={archiving} />}>Cancelar</DialogClose>
            <Button variant="destructive" onClick={handleArchive} disabled={archiving}>
              {archiving ? "Arquivando..." : "Arquivar"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

"use client";

import Link from "next/link";
import { useSetPageHeader } from "@/lib/page-header";
import { EmptyState } from "@/components/states";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { UserRow } from "@/lib/types";

/** Lista de membros da organização. Somente leitura aqui — ações vivem no detalhe. */
export function UsersList({ initial }: { initial: UserRow[] }) {
  useSetPageHeader({ title: "Usuários", breadcrumb: ["Administração", "Usuários"] });

  if (initial.length === 0) {
    return <EmptyState title="Nenhum usuário" description="Ainda não há membros nesta organização." />;
  }

  return (
    <div className="rounded-xl ring-1 ring-foreground/10">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Nome</TableHead>
            <TableHead>Email</TableHead>
            <TableHead>Perfis</TableHead>
            <TableHead>Empresas</TableHead>
            <TableHead>Status</TableHead>
            <TableHead className="text-right">Ações</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {initial.map((u) => (
            <TableRow key={u.id} data-testid={`user-row-${u.id}`}>
              <TableCell className="font-medium">
                <span className="flex items-center gap-2">
                  {u.fullName}
                  {u.isOwner && <Badge variant="info">Owner</Badge>}
                </span>
              </TableCell>
              <TableCell className="text-muted-foreground">{u.email}</TableCell>
              <TableCell>
                {u.profiles.length === 0 ? (
                  <span className="text-sm text-muted-foreground">— sem perfil —</span>
                ) : (
                  <span className="flex flex-wrap gap-1">
                    {u.profiles.map((p) => (
                      <Badge key={p.id}>{p.name}</Badge>
                    ))}
                  </span>
                )}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {u.companies.length === 0 ? "—" : u.companies.map((c) => c.name).join(", ")}
              </TableCell>
              <TableCell>
                {u.status.toLowerCase() === "active" ? (
                  <Badge variant="success">Ativo</Badge>
                ) : (
                  <Badge>Inativo</Badge>
                )}
              </TableCell>
              <TableCell className="text-right">
                <Button variant="outline" size="sm" render={<Link href={`/users/${u.id}`} />}>
                  Ver
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

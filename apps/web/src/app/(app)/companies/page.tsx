"use client";

import Link from "next/link";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import type { Company } from "@/lib/types";
import { useSession } from "@/lib/session";
import { useSetPageHeader } from "@/lib/page-header";
import { EmptyState, ErrorState } from "@/components/states";
import { Button } from "@/components/ui/button";
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

function StatusBadge({ status }: { status: string }) {
  const active = status.toLowerCase() === "active";
  return (
    <span
      className={
        "inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset " +
        (active
          ? "bg-emerald-50 text-emerald-700 ring-emerald-600/20 dark:bg-emerald-950 dark:text-emerald-400"
          : "bg-muted text-muted-foreground ring-border")
      }
    >
      {active ? "Ativa" : status}
    </span>
  );
}

export default function CompaniesPage() {
  useSetPageHeader({ title: "Empresas", breadcrumb: ["Administração", "Empresas"] });
  const { companies: sessionCompanies, orgRole } = useSession();
  const isAdmin = orgRole === "Admin";
  const [companies, setCompanies] = useState<Company[]>(sessionCompanies);
  const [error, setError] = useState<string | null>(null);
  const [target, setTarget] = useState<Company | null>(null);
  const [deleting, setDeleting] = useState(false);

  async function handleDelete() {
    if (!target) return;
    setDeleting(true);
    setError(null);
    try {
      await api.del<void>(`/companies/${target.id}`);
      setCompanies((prev) => prev.filter((c) => c.id !== target.id));
      setTarget(null);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      {isAdmin && (
        <div>
          <Button render={<Link href="/companies/new" />}>Nova empresa</Button>
        </div>
      )}

      {error && <ErrorState message={error} onRetry={() => setError(null)} />}

      {companies.length === 0 ? (
        <EmptyState
          title="Nenhuma empresa ainda"
          description={
            isAdmin
              ? "Crie a primeira empresa para começar."
              : "Peça a um administrador para conceder acesso a uma empresa."
          }
          action={
            isAdmin ? (
              <Button render={<Link href="/companies/new" />}>Nova empresa</Button>
            ) : undefined
          }
        />
      ) : (
        <div className="rounded-xl ring-1 ring-foreground/10">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead>Status</TableHead>
                {isAdmin && <TableHead className="text-right">Ações</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {companies.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-medium">{c.name}</TableCell>
                  <TableCell>
                    <StatusBadge status={c.status} />
                  </TableCell>
                  {isAdmin && (
                    <TableCell className="text-right">
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          render={<Link href={`/companies/${c.id}`} />}
                        >
                          Editar
                        </Button>
                        <Button variant="destructive" size="sm" onClick={() => setTarget(c)}>
                          Excluir
                        </Button>
                      </div>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog open={target !== null} onOpenChange={(open) => !open && setTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Excluir empresa</DialogTitle>
            <DialogDescription>
              Tem certeza que deseja excluir <strong>{target?.name}</strong>? Esta ação pode ser
              revertida apenas pela equipe.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose render={<Button variant="outline" disabled={deleting} />}>
              Cancelar
            </DialogClose>
            <Button variant="destructive" onClick={handleDelete} disabled={deleting}>
              {deleting ? "Excluindo..." : "Excluir"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

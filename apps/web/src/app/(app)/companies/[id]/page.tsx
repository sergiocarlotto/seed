"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { use, useEffect, useState } from "react";
import CompanyForm from "@/components/CompanyForm";
import { api, errorMessage } from "@/lib/api";
import type { Company } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

export default function EditCompanyPage({ params }: { params: Promise<{ id: string }> }) {
  // Next 16: `params` de páginas dinâmicas é uma Promise; em client component
  // usamos o hook `use` para resolvê-la.
  const { id } = use(params);
  const router = useRouter();
  const [company, setCompany] = useState<Company | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let active = true;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await api.get<Company>(`/companies/${id}`);
        if (active) setCompany(data);
      } catch (err) {
        if (active) setError(errorMessage(err));
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [id]);

  async function handleSubmit(name: string) {
    await api.put<Company>(`/companies/${id}`, { name });
    router.push("/companies");
    router.refresh();
  }

  async function handleDelete() {
    setDeleting(true);
    setError(null);
    try {
      await api.del<void>(`/companies/${id}`);
      router.push("/companies");
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
      setDeleting(false);
      setConfirmOpen(false);
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-sm flex-col gap-6 px-4 py-10">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Editar empresa</h1>
        <Button variant="ghost" size="sm" render={<Link href="/companies" />}>
          Voltar
        </Button>
      </div>

      {loading ? (
        <p className="text-sm text-muted-foreground">Carregando...</p>
      ) : error && !company ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : company ? (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Dados da empresa</CardTitle>
            </CardHeader>
            <CardContent>
              <CompanyForm initialName={company.name} submitLabel="Salvar" onSubmit={handleSubmit} />
            </CardContent>
          </Card>

          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}

          <div>
            <Button variant="destructive" onClick={() => setConfirmOpen(true)}>
              Excluir empresa
            </Button>
          </div>
        </>
      ) : null}

      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Excluir empresa</DialogTitle>
            <DialogDescription>
              Tem certeza que deseja excluir <strong>{company?.name}</strong>? Esta ação pode ser
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
    </main>
  );
}

"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { use, useEffect, useState } from "react";
import CompanyForm from "@/components/CompanyForm";
import { api, errorMessage } from "@/lib/api";
import type { Organization } from "@/lib/types";

export default function EditCompanyPage({ params }: { params: Promise<{ id: string }> }) {
  // Next 16: `params` de páginas dinâmicas é uma Promise; em client component
  // usamos o hook `use` para resolvê-la.
  const { id } = use(params);
  const router = useRouter();
  const [company, setCompany] = useState<Organization | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let active = true;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await api.get<Organization>(`/organizations/${id}`);
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
    await api.put<Organization>(`/organizations/${id}`, { name });
    router.push("/companies");
    router.refresh();
  }

  async function handleDelete() {
    if (!window.confirm("Excluir esta empresa? Esta ação não pode ser desfeita.")) return;
    setDeleting(true);
    setError(null);
    try {
      await api.del<void>(`/organizations/${id}`);
      router.push("/companies");
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
      setDeleting(false);
    }
  }

  return (
    <main className="mx-auto flex w-full max-w-sm flex-col gap-6 px-4 py-10">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Editar empresa</h1>
        <Link href="/companies" className="text-sm font-medium underline text-zinc-600 dark:text-zinc-400">
          Voltar
        </Link>
      </div>

      {loading ? (
        <p className="text-sm text-zinc-500 dark:text-zinc-400">Carregando...</p>
      ) : error && !company ? (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      ) : company ? (
        <>
          <CompanyForm initialName={company.name} submitLabel="Salvar" onSubmit={handleSubmit} />

          {error && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {error}
            </p>
          )}

          <button
            type="button"
            onClick={handleDelete}
            disabled={deleting}
            className="rounded border border-red-300 px-4 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
          >
            {deleting ? "Excluindo..." : "Excluir empresa"}
          </button>
        </>
      ) : null}
    </main>
  );
}

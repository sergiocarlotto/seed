"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { logout } from "@/lib/auth";
import type { Organization } from "@/lib/types";

export default function CompaniesPage() {
  const router = useRouter();
  const [companies, setCompanies] = useState<Organization[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const data = await api.get<Organization[]>("/organizations");
        if (active) setCompanies(data);
      } catch (err) {
        if (active) setError(errorMessage(err));
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  async function handleDelete(id: string) {
    if (!window.confirm("Excluir esta empresa? Esta ação não pode ser desfeita.")) return;
    setDeletingId(id);
    setError(null);
    try {
      await api.del<void>(`/organizations/${id}`);
      setCompanies((prev) => prev.filter((c) => c.id !== id));
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setDeletingId(null);
    }
  }

  async function handleLogout() {
    try {
      await logout();
    } catch {
      // Mesmo se falhar, seguimos para o login.
    }
    router.push("/login");
    router.refresh();
  }

  return (
    <main className="mx-auto flex w-full max-w-2xl flex-col gap-6 px-4 py-10">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Empresas</h1>
        <button
          type="button"
          onClick={handleLogout}
          className="rounded border border-zinc-300 px-3 py-1.5 text-sm font-medium text-zinc-700 hover:bg-zinc-100 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
        >
          Sair
        </button>
      </div>

      <div>
        <Link
          href="/companies/new"
          className="inline-block rounded bg-zinc-900 px-4 py-2 text-sm font-medium text-white dark:bg-zinc-50 dark:text-zinc-900"
        >
          Nova empresa
        </Link>
      </div>

      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      {loading ? (
        <p className="text-sm text-zinc-500 dark:text-zinc-400">Carregando...</p>
      ) : companies.length === 0 ? (
        <p className="text-sm text-zinc-500 dark:text-zinc-400">Nenhuma empresa ainda.</p>
      ) : (
        <ul className="flex flex-col divide-y divide-zinc-200 rounded border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
          {companies.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-4 px-4 py-3">
              <div className="flex flex-col">
                <span className="font-medium text-zinc-900 dark:text-zinc-50">{c.name}</span>
                <span className="text-xs text-zinc-500 dark:text-zinc-400">Papel: {c.role}</span>
              </div>
              <div className="flex items-center gap-2">
                <Link
                  href={`/companies/${c.id}`}
                  className="rounded border border-zinc-300 px-3 py-1.5 text-sm font-medium text-zinc-700 hover:bg-zinc-100 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  Editar
                </Link>
                <button
                  type="button"
                  onClick={() => handleDelete(c.id)}
                  disabled={deletingId === c.id}
                  className="rounded border border-red-300 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                >
                  {deletingId === c.id ? "Excluindo..." : "Excluir"}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}

"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import CompanyForm from "@/components/CompanyForm";
import { api } from "@/lib/api";
import type { Organization } from "@/lib/types";

export default function NewCompanyPage() {
  const router = useRouter();

  async function handleSubmit(name: string) {
    await api.post<Organization>("/organizations", { name });
    router.push("/companies");
    router.refresh();
  }

  return (
    <main className="mx-auto flex w-full max-w-sm flex-col gap-6 px-4 py-10">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900 dark:text-zinc-50">Nova empresa</h1>
        <Link href="/companies" className="text-sm font-medium underline text-zinc-600 dark:text-zinc-400">
          Voltar
        </Link>
      </div>

      <CompanyForm submitLabel="Criar" onSubmit={handleSubmit} />
    </main>
  );
}

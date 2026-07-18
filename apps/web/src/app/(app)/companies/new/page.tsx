"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import CompanyForm from "@/components/CompanyForm";
import { api } from "@/lib/api";
import type { Company } from "@/lib/types";
import { useSetPageHeader } from "@/lib/page-header";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function NewCompanyPage() {
  useSetPageHeader({ title: "Nova empresa", breadcrumb: ["Administração", "Empresas", "Nova"] });
  const router = useRouter();

  async function handleSubmit(name: string) {
    await api.post<Company>("/companies", { name });
    router.push("/companies");
    router.refresh();
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <div className="flex items-center justify-between gap-4">
        <h2 className="text-lg font-semibold">Dados da empresa</h2>
        <Button variant="ghost" size="sm" render={<Link href="/companies" />}>
          Voltar
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dados da empresa</CardTitle>
        </CardHeader>
        <CardContent>
          <CompanyForm submitLabel="Criar" onSubmit={handleSubmit} />
        </CardContent>
      </Card>
    </div>
  );
}

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { getMeServer } from "@/lib/api-server";
import { resolveActiveCompany, ACTIVE_COMPANY_COOKIE } from "@/lib/active-company";
import { SessionProvider } from "@/lib/session";
import { ActiveCompanyProvider } from "@/lib/active-company-context";
import { PageHeaderProvider } from "@/lib/page-header";
import { AppShell } from "@/components/shell/AppShell";
import type { Me } from "@/lib/types";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  // 1) Sessão + `me` (uma vez para toda tela autenticada).
  let me: Me;
  try {
    me = await getMeServer();
  } catch {
    // 401 (ou qualquer falha de sessão) → login. O enforcement real é do backend.
    redirect("/login");
  }

  // 2) Resolver a empresa ativa a partir do cookie, validada contra `me`.
  //    READ-ONLY: o Next 16 proíbe escrever cookies em Server Components/layouts.
  //    O fallback (primeira empresa por nome) é recalculado a cada request de forma
  //    determinística; o cookie só é gravado no cliente quando o usuário troca de
  //    empresa (ActiveCompanyProvider.switchCompany).
  const cookieStore = await cookies();
  const cookieId = cookieStore.get(ACTIVE_COMPANY_COOKIE)?.value;
  const { active } = resolveActiveCompany(me.companies, cookieId);

  // 3) Providers cliente + casco.
  return (
    <SessionProvider me={me}>
      <ActiveCompanyProvider active={active} companies={me.companies}>
        <PageHeaderProvider>
          <AppShell>{children}</AppShell>
        </PageHeaderProvider>
      </ActiveCompanyProvider>
    </SessionProvider>
  );
}

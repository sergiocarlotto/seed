"use client";

import { useRouter } from "next/navigation";
import { createContext, useCallback, useContext, type ReactNode } from "react";
import type { Company } from "./types";
import { ACTIVE_COMPANY_COOKIE } from "./active-company";

type ActiveCompanyContextValue = {
  /** Empresa ativa, ou null quando o usuário não tem nenhuma. */
  active: Company | null;
  /** Empresas acessíveis (para o seletor). */
  companies: Company[];
  /** Troca a empresa: grava o cookie e recarrega o escopo na mesma rota. */
  switchCompany: (id: string) => void;
};

const ActiveCompanyContext = createContext<ActiveCompanyContextValue | null>(null);

export function ActiveCompanyProvider({
  active,
  companies,
  children,
}: {
  active: Company | null;
  companies: Company[];
  children: ReactNode;
}) {
  const router = useRouter();

  const switchCompany = useCallback(
    (id: string) => {
      if (id === active?.id) return;
      const secure = process.env.NODE_ENV === "production" ? "; Secure" : "";
      // 1 ano de validade; SameSite=Lax para navegação normal.
      document.cookie = `${ACTIVE_COMPANY_COOKIE}=${id}; Path=/; Max-Age=31536000; SameSite=Lax${secure}`;
      // Re-renderiza os Server Components no novo escopo, sem trocar de rota.
      router.refresh();
    },
    [active?.id, router]
  );

  return (
    <ActiveCompanyContext.Provider value={{ active, companies, switchCompany }}>
      {children}
    </ActiveCompanyContext.Provider>
  );
}

export function useActiveCompany(): ActiveCompanyContextValue {
  const ctx = useContext(ActiveCompanyContext);
  if (ctx === null) {
    throw new Error("useActiveCompany precisa estar dentro de <ActiveCompanyProvider>");
  }
  return ctx;
}

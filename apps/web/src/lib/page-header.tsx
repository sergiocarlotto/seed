"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";

export type PageHeader = {
  title: string;
  /** Trilha opcional; o último item costuma repetir o título. */
  breadcrumb?: string[];
};

type PageHeaderContextValue = {
  header: PageHeader;
  setHeader: (header: PageHeader) => void;
};

const PageHeaderContext = createContext<PageHeaderContextValue | null>(null);

export function PageHeaderProvider({ children }: { children: ReactNode }) {
  const [header, setHeader] = useState<PageHeader>({ title: "" });
  return (
    <PageHeaderContext.Provider value={{ header, setHeader }}>
      {children}
    </PageHeaderContext.Provider>
  );
}

/** A topbar lê o cabeçalho atual. */
export function usePageHeader(): PageHeader {
  const ctx = useContext(PageHeaderContext);
  if (ctx === null) {
    throw new Error("usePageHeader precisa estar dentro de <PageHeaderProvider>");
  }
  return ctx.header;
}

/** Cada página declara seu título/breadcrumb chamando este hook uma vez. */
export function useSetPageHeader(header: PageHeader): void {
  const ctx = useContext(PageHeaderContext);
  if (ctx === null) {
    throw new Error("useSetPageHeader precisa estar dentro de <PageHeaderProvider>");
  }
  const { setHeader } = ctx;
  const key = `${header.title}|${(header.breadcrumb ?? []).join(">")}`;
  useEffect(() => {
    setHeader(header);
    // `key` resume o conteúdo; evita re-set a cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, setHeader]);
}

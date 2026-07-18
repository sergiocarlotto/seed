"use client";

import { createContext, useContext, type ReactNode } from "react";
import type { Me } from "./types";

const SessionContext = createContext<Me | null>(null);

export function SessionProvider({ me, children }: { me: Me; children: ReactNode }) {
  return <SessionContext.Provider value={me}>{children}</SessionContext.Provider>;
}

/** `me` sempre presente dentro do `(app)` — o layout servidor garante. */
export function useSession(): Me {
  const ctx = useContext(SessionContext);
  if (ctx === null) {
    throw new Error("useSession precisa estar dentro de <SessionProvider>");
  }
  return ctx;
}

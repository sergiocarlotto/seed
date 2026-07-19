"use client";

import { useRouter } from "next/navigation";
import { useCallback } from "react";
import { logout } from "./auth";

/** Faz logout e leva ao /login, seguindo mesmo se a chamada à API falhar. */
export function useLogout() {
  const router = useRouter();
  return useCallback(async () => {
    try {
      await logout();
    } catch {
      // Mesmo se falhar, seguimos para o login.
    }
    router.push("/login");
    router.refresh();
  }, [router]);
}

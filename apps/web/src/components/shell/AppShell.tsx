"use client";

import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";
import { AppSidebar } from "./AppSidebar";
import { AppTopbar } from "./AppTopbar";
import { MobileDrawer } from "./MobileDrawer";

const COLLAPSE_KEY = "app-shell:sidebar-collapsed";

/**
 * Casco cliente: sidebar fixa (desktop) + topbar + conteúdo, e gaveta (mobile).
 * O botão de menu recolhe/expande no desktop e abre a gaveta no mobile. A
 * preferência de recolhido persiste em localStorage.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Lê a preferência persistida após montar (evita mismatch de hidratação).
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- sincroniza com localStorage (externo), não com props/state; ler no initializer quebraria no SSR.
    setCollapsed(localStorage.getItem(COLLAPSE_KEY) === "1");
  }, []);

  function toggleCollapsed() {
    setCollapsed((prev) => {
      const next = !prev;
      localStorage.setItem(COLLAPSE_KEY, next ? "1" : "0");
      return next;
    });
  }

  // Desktop: alterna recolhido. Mobile: a mesma tecla abre a gaveta. Como o
  // desktop esconde o botão-gaveta via CSS, tratamos ambos: no desktop o
  // `lg:` da sidebar decide a exibição; o toggle afeta a largura.
  function handleMenuClick() {
    // Abre a gaveta no mobile e alterna recolhido no desktop.
    if (window.matchMedia("(min-width: 1024px)").matches) {
      toggleCollapsed();
    } else {
      setDrawerOpen(true);
    }
  }

  return (
    <div className="flex h-full">
      {/* Sidebar fixa só no desktop (lg+) */}
      <aside
        className={cn(
          "hidden shrink-0 border-r lg:block transition-[width]",
          collapsed ? "w-16" : "w-60"
        )}
        data-testid="desktop-sidebar"
      >
        <div className="sticky top-0 h-screen">
          <AppSidebar collapsed={collapsed} />
        </div>
      </aside>

      {/* Gaveta mobile */}
      <MobileDrawer open={drawerOpen} onOpenChange={setDrawerOpen} />

      {/* Coluna principal */}
      <div className="flex min-w-0 flex-1 flex-col">
        <AppTopbar onMenuClick={handleMenuClick} />
        <main className="flex-1 overflow-y-auto">
          <div className="mx-auto w-full max-w-6xl px-4 py-6 sm:px-6">{children}</div>
        </main>
      </div>
    </div>
  );
}

"use client";

import { Menu, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CompanySwitcher } from "./CompanySwitcher";
import { UserMenu } from "./UserMenu";
import { usePageHeader } from "@/lib/page-header";

/**
 * Topbar única. O botão de menu recolhe a sidebar (desktop) ou abre a gaveta
 * (mobile). Título/breadcrumb vêm do contexto de cabeçalho que cada página
 * preenche. Busca é visual e desabilitada; some no mobile.
 */
export function AppTopbar({ onMenuClick }: { onMenuClick: () => void }) {
  const header = usePageHeader();

  return (
    <header className="flex h-14 items-center gap-3 border-b bg-background px-3 sm:px-4">
      <Button
        variant="ghost"
        size="icon-sm"
        onClick={onMenuClick}
        aria-label="Alternar menu"
        data-testid="menu-toggle"
      >
        <Menu className="size-5" />
      </Button>

      <div className="flex min-w-0 flex-col">
        <h1 className="truncate text-sm font-semibold leading-tight" data-testid="page-title">
          {header.title}
        </h1>
        {header.breadcrumb && header.breadcrumb.length > 0 && (
          <p className="hidden truncate text-xs text-muted-foreground md:block">
            {header.breadcrumb.join(" / ")}
          </p>
        )}
      </div>

      {/* Busca desabilitada, ancora o layout; some no mobile. */}
      <div className="ml-auto hidden md:block">
        <div className="relative">
          <Search className="pointer-events-none absolute left-2 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            disabled
            placeholder="Pesquisar no sistema"
            aria-label="Pesquisar no sistema (em breve)"
            className="w-48 pl-8 lg:w-64"
          />
        </div>
      </div>

      <div className="ml-auto flex items-center gap-2 md:ml-0">
        <CompanySwitcher />
        <UserMenu />
      </div>
    </header>
  );
}

"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { HelpCircle, LogOut, Sprout } from "lucide-react";
import { useRouter } from "next/navigation";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { navModules, visibleNav, type OrgRole } from "@/lib/nav";
import { useSession } from "@/lib/session";
import { logout } from "@/lib/auth";

/**
 * Menu lateral gerado a partir de `navModules`, filtrado pelo papel do usuário.
 * `collapsed` mostra só ícones (rótulos em tooltip). Em mobile é renderizado
 * dentro da gaveta; `onNavigate` fecha a gaveta ao escolher um item.
 */
export function AppSidebar({
  collapsed = false,
  onNavigate,
}: {
  collapsed?: boolean;
  onNavigate?: () => void;
}) {
  const pathname = usePathname();
  const router = useRouter();
  const { orgRole } = useSession();
  const modules = visibleNav(navModules, orgRole as OrgRole);

  async function handleLogout() {
    try {
      await logout();
    } catch {
      // segue para o login mesmo em falha
    }
    router.push("/login");
    router.refresh();
  }

  return (
    <TooltipProvider>
      <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
        {/* Topo: logo + nome */}
        <div className={cn("flex h-14 items-center gap-2 px-4", collapsed && "justify-center px-0")}>
          <Sprout className="size-5 shrink-0 text-primary" />
          {!collapsed && <span className="font-heading font-semibold">Seed</span>}
        </div>

        {/* Corpo: módulos sempre visíveis */}
        <nav className="flex-1 overflow-y-auto px-2 py-2">
          {modules.map((mod) => (
            <div key={mod.label} className="mb-4">
              {!collapsed && (
                <p className="px-2 pb-1 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                  {mod.label}
                </p>
              )}
              <ul className="flex flex-col gap-0.5">
                {mod.items.map((item) => {
                  const active = pathname === item.href || pathname.startsWith(item.href + "/");
                  const Icon = item.icon;
                  const link = (
                    <Link
                      href={item.href}
                      onClick={onNavigate}
                      aria-current={active ? "page" : undefined}
                      data-testid={`nav-${item.href}`}
                      className={cn(
                        "flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm transition-colors",
                        collapsed && "justify-center",
                        active
                          ? "bg-sidebar-accent font-medium text-sidebar-accent-foreground"
                          : "text-sidebar-foreground/80 hover:bg-sidebar-accent/60"
                      )}
                    >
                      <Icon className="size-4 shrink-0" />
                      {!collapsed && <span className="truncate">{item.label}</span>}
                    </Link>
                  );
                  return (
                    <li key={item.href}>
                      {collapsed ? (
                        <Tooltip>
                          <TooltipTrigger render={link} />
                          <TooltipContent side="right">{item.label}</TooltipContent>
                        </Tooltip>
                      ) : (
                        link
                      )}
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </nav>

        {/* Rodapé: Ajuda (reservado) + Sair (funcional) */}
        <div className="border-t p-2">
          <Button
            variant="ghost"
            size="sm"
            disabled
            className={cn("w-full justify-start", collapsed && "justify-center")}
          >
            <HelpCircle className="size-4" />
            {!collapsed && "Ajuda"}
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={handleLogout}
            data-testid="sidebar-logout"
            className={cn("w-full justify-start", collapsed && "justify-center")}
          >
            <LogOut className="size-4" />
            {!collapsed && "Sair"}
          </Button>
        </div>
      </div>
    </TooltipProvider>
  );
}

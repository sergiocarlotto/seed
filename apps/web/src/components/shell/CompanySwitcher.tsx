"use client";

import { Building2, Check, ChevronsUpDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { buttonVariants } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useActiveCompany } from "@/lib/active-company-context";

export function CompanySwitcher() {
  const { active, companies, switchCompany } = useActiveCompany();

  // Usuário sem nenhuma empresa: estado vazio, sem opções.
  if (companies.length === 0) {
    return (
      <span className="text-sm text-muted-foreground" data-testid="company-switcher-empty">
        Nenhuma empresa disponível
      </span>
    );
  }

  return (
    <DropdownMenu>
      {/* O gatilho é o próprio <button> nativo do Base UI, estilizado como Button
          (evita aninhar dois componentes-botão do Base UI). */}
      <DropdownMenuTrigger
        data-testid="company-switcher"
        className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
      >
        <Building2 />
        <span className="max-w-[10rem] truncate">{active?.name ?? "Selecionar"}</span>
        <ChevronsUpDown className="opacity-60" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel>Empresa ativa</DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        {companies.map((c) => (
          <DropdownMenuItem
            key={c.id}
            onClick={() => switchCompany(c.id)}
            data-testid={`company-option-${c.id}`}
          >
            <span className="truncate">{c.name}</span>
            {c.id === active?.id && <Check className="ml-auto size-4" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

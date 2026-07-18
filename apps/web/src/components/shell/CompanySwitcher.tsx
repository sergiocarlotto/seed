"use client";

import { Building2, Check, ChevronsUpDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
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
      <DropdownMenuTrigger
        render={
          <Button variant="outline" size="sm" data-testid="company-switcher">
            <Building2 />
            <span className="max-w-[10rem] truncate">{active?.name ?? "Selecionar"}</span>
            <ChevronsUpDown className="opacity-60" />
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>Empresa ativa</DropdownMenuLabel>
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

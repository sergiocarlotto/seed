"use client";

import { LogOut, User } from "lucide-react";
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
import { useSession } from "@/lib/session";
import { useLogout } from "@/lib/use-logout";

export function UserMenu() {
  const { user, orgRole } = useSession();
  const handleLogout = useLogout();

  const initials = user.fullName
    .split(" ")
    .map((p) => p[0])
    .slice(0, 2)
    .join("")
    .toUpperCase();

  return (
    <DropdownMenu>
      {/* O gatilho é o próprio <button> nativo do Base UI, estilizado como Button
          (evita aninhar dois componentes-botão do Base UI). */}
      <DropdownMenuTrigger
        data-testid="user-menu"
        aria-label={user.fullName}
        className={cn(buttonVariants({ variant: "ghost", size: "sm" }))}
      >
        <span className="flex size-6 items-center justify-center rounded-full bg-muted text-xs font-medium">
          {initials || <User className="size-4" />}
        </span>
        <span className="hidden max-w-[8rem] truncate sm:inline">{user.fullName}</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuGroup>
          <DropdownMenuLabel>
            <div className="flex flex-col">
              <span className="truncate font-medium">{user.fullName}</span>
              <span className="truncate text-xs font-normal text-muted-foreground">
                {user.email}
              </span>
              <span className="text-xs font-normal text-muted-foreground">{orgRole}</span>
            </div>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        {/* Itens futuros: acinzentados/desabilitados, sinalizam que virão. */}
        <DropdownMenuItem disabled>Minha conta</DropdownMenuItem>
        <DropdownMenuItem disabled>Preferências</DropdownMenuItem>
        <DropdownMenuItem disabled>Alterar senha</DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={handleLogout} data-testid="logout">
          <LogOut className="size-4" />
          Sair
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

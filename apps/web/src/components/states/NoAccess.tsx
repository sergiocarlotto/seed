import Link from "next/link";
import { Lock } from "lucide-react";
import { Button } from "@/components/ui/button";

/**
 * Par visual do 403/404 do backend, renderizado DENTRO da área de conteúdo
 * (menu e topbar permanecem visíveis). Oferece caminho de volta.
 */
export function NoAccess({
  title = "Sem acesso",
  message = "Você não tem permissão para ver este recurso ou ele pertence a outra empresa.",
  backHref = "/companies",
  backLabel = "Voltar",
}: {
  title?: string;
  message?: string;
  backHref?: string;
  backLabel?: string;
}) {
  return (
    <div
      role="status"
      className="flex flex-col items-center gap-3 rounded-xl border border-dashed p-8 text-center"
    >
      <Lock className="size-8 text-muted-foreground" />
      <div className="flex flex-col gap-1">
        <p className="font-medium">{title}</p>
        <p className="text-sm text-muted-foreground">{message}</p>
      </div>
      <Button variant="outline" size="sm" render={<Link href={backHref} />}>
        {backLabel}
      </Button>
    </div>
  );
}

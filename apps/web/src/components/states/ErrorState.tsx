import { AlertCircle } from "lucide-react";
import { Button } from "@/components/ui/button";

/**
 * Mensagem amigável + causa opcional + "Tentar novamente".
 * Passe `message` já resolvido por `errorMessage()` de `lib/api.ts`.
 */
export function ErrorState({
  title = "Algo deu errado",
  message,
  onRetry,
}: {
  title?: string;
  message?: string;
  onRetry?: () => void;
}) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center gap-3 rounded-xl border border-dashed p-8 text-center"
    >
      <AlertCircle className="size-8 text-destructive" />
      <div className="flex flex-col gap-1">
        <p className="font-medium">{title}</p>
        {message && <p className="text-sm text-muted-foreground">{message}</p>}
      </div>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          Tentar novamente
        </Button>
      )}
    </div>
  );
}

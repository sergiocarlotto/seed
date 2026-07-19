import { cn } from "@/lib/utils";

/**
 * Skeletons para layouts previsíveis. `rows` controla quantas linhas imitar.
 * Use `label` para o caso genérico (spinner textual acessível).
 */
export function Loading({
  rows = 3,
  label,
  className,
}: {
  rows?: number;
  label?: string;
  className?: string;
}) {
  if (label) {
    return (
      <p role="status" className={cn("text-sm text-muted-foreground", className)}>
        {label}
      </p>
    );
  }
  return (
    <div role="status" aria-label="Carregando" className={cn("flex flex-col gap-3", className)}>
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="h-10 w-full animate-pulse rounded-lg bg-muted" />
      ))}
    </div>
  );
}

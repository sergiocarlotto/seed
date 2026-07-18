"use client";

import { useState } from "react";

type CompanyFormProps = {
  initialName?: string;
  submitLabel: string;
  onSubmit: (name: string) => Promise<void>;
};

export default function CompanyForm({ initialName = "", submitLabel, onSubmit }: CompanyFormProps) {
  const [name, setName] = useState(initialName);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await onSubmit(name.trim());
    } catch (err) {
      setError(err instanceof Error ? err.message : String((err as { message?: string })?.message ?? "Erro ao salvar."));
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="flex flex-col gap-1">
        <label htmlFor="name" className="text-sm font-medium text-zinc-700 dark:text-zinc-300">
          Nome da empresa
        </label>
        <input
          id="name"
          type="text"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="rounded border border-zinc-300 px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900"
        />
      </div>

      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      <button
        type="submit"
        disabled={loading}
        className="rounded bg-zinc-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50 dark:bg-zinc-50 dark:text-zinc-900"
      >
        {loading ? "Salvando..." : submitLabel}
      </button>
    </form>
  );
}

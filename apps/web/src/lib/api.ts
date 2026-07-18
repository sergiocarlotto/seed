export type ApiError = { status: number; message: string };

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`/api${path}`, {
    method,
    credentials: "include",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    let message = res.statusText;
    try {
      const j = await res.json();
      message = j.error ?? j.errors?.[0] ?? message;
    } catch {
      // corpo vazio ou não-JSON: mantém o statusText.
    }
    throw { status: res.status, message } as ApiError;
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

export const api = {
  get: <T>(p: string) => request<T>("GET", p),
  post: <T>(p: string, b?: unknown) => request<T>("POST", p, b),
  put: <T>(p: string, b?: unknown) => request<T>("PUT", p, b),
  del: <T>(p: string) => request<T>("DELETE", p),
};

export function isApiError(e: unknown): e is ApiError {
  return typeof e === "object" && e !== null && "status" in e && "message" in e;
}

export function errorMessage(e: unknown): string {
  return isApiError(e) ? e.message : "Erro inesperado. Tente novamente.";
}

import { cookies } from "next/headers";
import type { Me } from "./types";

// URL interna da API para chamadas server-side. Em dev híbrido: localhost:8080.
// No Docker, o serviço `web` recebe INTERNAL_API_URL=http://api:8080.
const API_BASE = process.env.INTERNAL_API_URL ?? "http://localhost:8080";

export type ServerApiError = { status: number; message: string };

/** GET server-side com repasse do cookie de sessão. Lança ServerApiError. */
export async function serverGet<T>(path: string): Promise<T> {
  const cookieHeader = (await cookies()).toString();
  const res = await fetch(`${API_BASE}${path}`, {
    method: "GET",
    headers: cookieHeader ? { cookie: cookieHeader } : undefined,
    cache: "no-store",
  });
  if (!res.ok) {
    throw { status: res.status, message: res.statusText } as ServerApiError;
  }
  return (res.status === 204 ? undefined : await res.json()) as T;
}

/** Busca o usuário atual. Lança ServerApiError com status 401 se não autenticado. */
export function getMeServer(): Promise<Me> {
  return serverGet<Me>("/auth/me");
}

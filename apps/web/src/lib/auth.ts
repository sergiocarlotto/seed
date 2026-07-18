import { api } from "./api";
import type { Me } from "./types";

export function getMe(): Promise<Me> {
  return api.get<Me>("/auth/me");
}

export function logout(): Promise<void> {
  return api.post<void>("/auth/logout");
}

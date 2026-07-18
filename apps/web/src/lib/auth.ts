import { api } from "./api";

export function logout(): Promise<void> {
  return api.post<void>("/auth/logout");
}

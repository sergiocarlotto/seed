import { api } from "./api";
import type { Membership, User } from "./types";

export type MeResponse = { user: User; memberships: Membership[] };

export function getMe(): Promise<MeResponse> {
  return api.get<MeResponse>("/auth/me");
}

export function logout(): Promise<void> {
  return api.post<void>("/auth/logout");
}

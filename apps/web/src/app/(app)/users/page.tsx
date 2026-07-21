import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { UsersList } from "@/components/UsersList";
import type { UserRow } from "@/lib/types";

export default async function UsersPage() {
  let users: UserRow[];
  try {
    users = await serverGet<UserRow[]>("/users");
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar os usuários." />;
  }
  return <UsersList initial={users} />;
}

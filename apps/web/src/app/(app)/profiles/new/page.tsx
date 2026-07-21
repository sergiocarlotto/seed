import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { ProfileForm } from "@/components/ProfileForm";
import type { PermissionGroup } from "@/lib/types";

export default async function NewProfilePage() {
  let groups: PermissionGroup[];
  try {
    groups = await serverGet<PermissionGroup[]>("/permissions");
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar as permissões." />;
  }
  return <ProfileForm mode="create" groups={groups} />;
}

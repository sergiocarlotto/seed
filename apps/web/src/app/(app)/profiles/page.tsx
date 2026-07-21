import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { ProfilesList } from "@/components/ProfilesList";
import type { ProfileSummary } from "@/lib/types";

// Server Component: prefetch da lista (abordagem A). O enforcement real é do
// backend; aqui 403 vira NoAccess (acesso direto por URL sem profiles.manage).
export default async function ProfilesPage() {
  let profiles: ProfileSummary[];
  try {
    profiles = await serverGet<ProfileSummary[]>("/profiles");
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar os perfis." />;
  }
  return <ProfilesList initial={profiles} />;
}

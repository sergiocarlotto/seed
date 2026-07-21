import { notFound } from "next/navigation";
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { UserDetail } from "@/components/UserDetail";
import type { UserRow, ProfileSummary } from "@/lib/types";

export default async function UserDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  let user: UserRow;
  try {
    user = await serverGet<UserRow>(`/users/${id}`);
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 404) notFound();
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar o usuário." />;
  }
  // Catálogo de perfis para o checklist — best-effort (exige profiles.manage).
  // Se falhar, a seção de perfis degrada para leitura (perfis atuais em chips).
  let allProfiles: ProfileSummary[] | null = null;
  try {
    allProfiles = await serverGet<ProfileSummary[]>("/profiles");
  } catch {
    allProfiles = null;
  }
  return <UserDetail user={user} allProfiles={allProfiles} />;
}

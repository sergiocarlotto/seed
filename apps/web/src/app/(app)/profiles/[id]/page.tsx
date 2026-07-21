import { notFound } from "next/navigation";
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { ProfileForm } from "@/components/ProfileForm";
import type { PermissionGroup, ProfileDetail } from "@/lib/types";

export default async function EditProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  let groups: PermissionGroup[];
  let profile: ProfileDetail;
  try {
    [groups, profile] = await Promise.all([
      serverGet<PermissionGroup[]>("/permissions"),
      serverGet<ProfileDetail>(`/profiles/${id}`),
    ]);
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 404) notFound();
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar o perfil." />;
  }
  return <ProfileForm mode="edit" groups={groups} profile={profile} />;
}

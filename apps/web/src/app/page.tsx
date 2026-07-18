import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export default async function Home() {
  // Next 16: `cookies()` é assíncrono. Check otimista de sessão para escolher
  // o destino inicial; o enforcement real é feito no backend.
  const cookieStore = await cookies();
  const hasSession = cookieStore.has(".AspNetCore.Identity.Application");
  redirect(hasSession ? "/companies" : "/login");
}

import { NextResponse, type NextRequest } from "next/server";

// Next 16: o antigo `middleware` foi renomeado para `proxy`. Este proxy faz um
// check otimista de sessão (presença do cookie do Identity) e redireciona para
// /login quando ausente. A barreira real de segurança é o backend.
export function proxy(req: NextRequest) {
  const hasSession = req.cookies.has(".AspNetCore.Identity.Application");
  if (!hasSession) {
    return NextResponse.redirect(new URL("/login", req.url));
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/companies", "/companies/:path*"],
};

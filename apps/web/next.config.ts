import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produces a minimal, self-contained server for Docker (see apps/web/Dockerfile).
  output: "standalone",
  async rewrites() {
    // Em produção o Caddy faz o same-origin (/api -> API), então o rewrite é
    // desnecessário e apontar para localhost:8080 dentro do container quebraria.
    if (process.env.NODE_ENV === "production") return [];
    // Em dev (fora do Docker), encaminha /api para a API local.
    return [{ source: "/api/:path*", destination: "http://localhost:8080/:path*" }];
  },
};

export default nextConfig;

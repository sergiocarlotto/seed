import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produces a minimal, self-contained server for Docker (see apps/web/Dockerfile).
  output: "standalone",
};

export default nextConfig;

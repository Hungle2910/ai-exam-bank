import type { NextConfig } from "next";

const backendUrl = (process.env.AIEXAMBANK_BACKEND_URL ?? "http://localhost:5000").replace(/\/+$/, "");

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${backendUrl}/api/:path*`,
      },
      {
        source: "/auth/:path*",
        destination: `${backendUrl}/auth/:path*`,
      },
      {
        source: "/health/:path*",
        destination: `${backendUrl}/health/:path*`,
      },
    ];
  },
};

export default nextConfig;

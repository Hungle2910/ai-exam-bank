import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: "http://localhost:5000/api/:path*",
      },
      {
        source: "/auth/:path*",
        destination: "http://localhost:5000/auth/:path*",
      },
      {
        source: "/health/:path*",
        destination: "http://localhost:5000/health/:path*",
      },
    ];
  },
};

export default nextConfig;

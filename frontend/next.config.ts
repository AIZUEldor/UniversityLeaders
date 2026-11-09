import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  poweredByHeader: false,
  // The app is never embedded: blocks clickjacking of admin actions (approve, deactivate, provision).
  async headers() {
    return [{
      source: "/:path*",
      headers: [
        { key: "X-Frame-Options", value: "DENY" },
        { key: "Content-Security-Policy", value: "frame-ancestors 'none'" },
        { key: "X-Content-Type-Options", value: "nosniff" },
        { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
        // Ignored by browsers over plain http (local dev); enforces https once served over TLS.
        { key: "Strict-Transport-Security", value: "max-age=31536000" },
      ],
    }];
  },
  // Same-origin deployment on free subdomains: the browser talks only to this site, which proxies the API, so the
  // session cookie is first-party. Set API_PROXY_TARGET (build time) and NEXT_PUBLIC_API_URL to this site's own origin.
  // Unset (local dev, custom domains): no proxy, the browser calls NEXT_PUBLIC_API_URL directly.
  async rewrites() {
    const target = process.env.API_PROXY_TARGET?.replace(/\/+$/, "");
    return target ? [
      { source: "/api/:path*", destination: `${target}/api/:path*` },
      { source: "/hubs/:path*", destination: `${target}/hubs/:path*` },
    ] : [];
  },
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;

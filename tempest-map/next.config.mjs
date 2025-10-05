import { defineConfig } from "next";

const config = defineConfig({
  experimental: {
    turbo: true,
    serverActions: {
      bodySizeLimit: "1mb"
    }
  },
  poweredByHeader: false,
  reactStrictMode: true,
  typescript: {
    ignoreBuildErrors: false
  }
});

export default config;

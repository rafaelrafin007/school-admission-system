import path from "node:path";
import { fileURLToPath } from "node:url";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

const rootDir = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, rootDir, "");
  const apiTarget =
    process.env.VITE_API_PROXY_TARGET ||
    env.VITE_API_PROXY_TARGET ||
    "http://127.0.0.1:8080";

  const apiProxy = {
    "/classes": {
      target: apiTarget,
      changeOrigin: true,
    },
    "/holds": {
      target: apiTarget,
      changeOrigin: true,
    },
    "/me": {
      target: apiTarget,
      changeOrigin: true,
    },
    "/health": {
      target: apiTarget,
      changeOrigin: true,
    },
  };

  return {
    root: rootDir,
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        "@": path.resolve(rootDir, "src"),
      },
    },
    server: {
      host: "127.0.0.1",
      port: 5173,
      strictPort: true,
      proxy: apiProxy,
    },
    preview: {
      port: 4173,
      proxy: apiProxy,
    },
  };
});

import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react-swc";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  // Load env file based on `mode` in the current working directory.
  // Set the third parameter to '' to load all env regardless of the `VITE_` prefix.
  const env = loadEnv(mode, "./", "");

  function getAspNetCoreTarget() {
    const httpsPort = env.VITE_ASPNETCORE_HTTPS_PORT;
    const urls = env.VITE_ASPNETCORE_URLS;
    if (httpsPort) {
      return `https://localhost:${httpsPort}`;
    }
    if (urls) {
      return urls.split(";")[0];
    }
    console.warn(
      "Warning: VITE_ASPNETCORE_HTTPS_PORT and VITE_ASPNETCORE_URLS are not set. Using default https://localhost:5001"
    );
    return "https://localhost:5001";
  }

  // https://vite.dev/config/
  return {
    plugins: [react()],
    server: {
  // Expose dev server on LAN so it's accessible via your local IP
  host: "0.0.0.0",
      port: 5173,
      proxy: {
        "/api": {
          target: getAspNetCoreTarget(),
          changeOrigin: true,
          secure: false,
          headers: {
            Connection: "Keep-Alive",
          },
        },
        "/Identity": {
          target: getAspNetCoreTarget(),
          changeOrigin: true,
          secure: false,
          headers: {
            Connection: "Keep-Alive",
          },
        },
        "/uploads": {
          target: "https://localhost:5001",
          secure: false,
          changeOrigin: true,
          headers: {
            Connection: "Keep-Alive",
          },
        },
        "/weatherforecast": {
          target: getAspNetCoreTarget(),
          changeOrigin: true,
          secure: false,
          headers: {
            Connection: "Keep-Alive",
          },
        },
        "/WeatherForecast": {
          target: getAspNetCoreTarget(),
          changeOrigin: true,
          secure: false,
          headers: {
            Connection: "Keep-Alive",
          },
        },
      },
    },
    // Optional: also expose preview server on LAN (vite preview)
    preview: {
      host: "0.0.0.0",
      port: 5173,
    },
    build: {
      outDir: "../wwwroot",
      emptyOutDir: true,
    },
  };
});

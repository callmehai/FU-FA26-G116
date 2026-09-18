import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Dev: Vite serves on :5173 and proxies the backend paths to ASP.NET Core (:5225), so the browser
// only ever sees a single origin — no CORS configuration needed.
// Build: emit straight into the backend's wwwroot so `dotnet run` can serve the production bundle.
const BACKEND = 'http://localhost:5225'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: BACKEND, changeOrigin: true },
      // SignalR upgrades to WebSocket, so `ws` has to be enabled.
      '/hubs': { target: BACKEND, changeOrigin: true, ws: true },
    },
  },
  build: {
    outDir: '../backend/TrustCabinet.Web/wwwroot',
    emptyOutDir: true,
  },
})

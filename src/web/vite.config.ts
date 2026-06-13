import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// dev: proxy /api to the local .NET API (dotnet run on :5266).
// build: emit to ./dist; the Docker build copies dist into the API's wwwroot so
// the single Render web service serves both the SPA and the API.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5266',
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})

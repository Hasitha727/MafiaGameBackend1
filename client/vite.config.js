import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: path.resolve(__dirname, '../src/MafiaGame.API/wwwroot'),
    emptyOutDir: true
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5019',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://localhost:5019',
        ws: true,
        changeOrigin: true
      }
    }
  }
});

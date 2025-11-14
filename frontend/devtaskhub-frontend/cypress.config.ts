import { defineConfig } from "cypress";

export default defineConfig({
  e2e: {
    baseUrl: "http://localhost:5173", // puerto de Vite
    viewportWidth: 1280,
    viewportHeight: 720,
    video: false,
  },
});

import { defineConfig } from 'cypress';
import { createViteBundler } from '@cypress/vite-dev-server';
import react from '@vitejs/plugin-react';

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:5173',
    specPattern: 'cypress/e2e/**/*.cy.ts',
    supportFile: 'cypress/support/e2e.ts',
    viewportWidth: 1280,
    viewportHeight: 720,
    video: false,
    setupNodeEvents(on, config) {
      on('dev-server:start', async (options) => {
        return createViteBundler({
          config: {
            plugins: [react()]
          }
        })(options);
      });
      return config;
    }
  }
});

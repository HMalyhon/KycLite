import { loadEnv } from 'vite'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // Dev proxy forwards /api to the ASP.NET Core backend so the browser sees a single origin.
  const apiTarget = env.VITE_API_TARGET || 'http://localhost:5000'

  return {
    plugins: [vue()],
    server: {
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
        },
      },
    },
    test: {
      // Component and composable tests mount real Vue components, so they need a DOM. The
      // dateParam tests are environment-agnostic and are unaffected.
      environment: 'jsdom',
      // jsdom implements neither of these, and UploadCard uses both for its image preview.
      setupFiles: ['./src/test/setup.ts'],
    },
  }
})

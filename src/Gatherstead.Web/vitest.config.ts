import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

// Web root — used to resolve Nuxt's `~`/`~~`/`@`/`@@` path aliases for imports under test.
const root = fileURLToPath(new URL('.', import.meta.url))

// Server-side utilities (the BFF token/session layer) are plain TypeScript modules whose Nuxt
// auto-imports ($fetch, createError, useRuntimeConfig, useStorage, getUserSession) are only invoked
// inside functions — so the pure helpers can be unit-tested under a plain node environment with the
// aliases resolved. Component/DOM tests can add `environment: 'happy-dom'` per-file via a docblock.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['tests/**/*.spec.ts'],
    globals: true,
  },
  resolve: {
    alias: {
      '~~': root,
      '@@': root,
      '~': root,
      '@': root,
    },
  },
})

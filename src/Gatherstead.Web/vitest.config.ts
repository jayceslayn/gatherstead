import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

// Web root — used to resolve Nuxt's `~`/`~~`/`@`/`@@` path aliases for imports under test.
const root = fileURLToPath(new URL('.', import.meta.url))

// Server-side utilities (the BFF token/session layer) are plain TypeScript modules whose Nuxt
// auto-imports ($fetch, createError, useRuntimeConfig, useStorage, getUserSession) are only invoked
// inside functions — so the pure helpers can be unit-tested under a plain node environment with the
// aliases resolved. If component/DOM tests are added later, add a vetted DOM environment (e.g. jsdom)
// as a devDependency then — deliberately not shipping one now to keep the dependency surface minimal.
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
      // Nuxt 4 maps `~`/`@` to srcDir (app/), not the package root.
      '~': `${root}app`,
      '@': `${root}app`,
    },
  },
})

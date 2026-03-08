// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',

  devtools: { enabled: true },

  typescript: {
    strict: true,
    typeCheck: 'build',
  },

  runtimeConfig: {
    public: {
      apiBaseUrl: 'http://localhost:5000',
    },
  },
})

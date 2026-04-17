// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',

  devtools: { enabled: true },

  css: [
    '~/assets/css/main.css',
  ],

  modules: [
    '@nuxt/ui',
    '@nuxt/fonts',
    '@nuxt/icon',
    '@nuxt/image',
    '@pinia/nuxt',
    'nuxt-auth-utils',
    '@nuxtjs/i18n',
  ],

  typescript: {
    strict: true,
    typeCheck: 'build',
  },

  routeRules: {
    '/': { prerender: true },
    '/tenants/**': { ssr: false },
  },

  i18n: {
    defaultLocale: 'en',
    langDir: 'locales',
    strategy: 'prefix_except_default',
    locales: [
      { code: 'en', language: 'en-US', file: 'en.json', name: 'English' },
      { code: 'es', language: 'es-ES', file: 'es.json', name: 'Español' },
    ],
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'i18n_locale',
      fallbackLocale: 'en',
      redirectOn: 'root',
    },
    vueI18n: './i18n.config.ts',
  },

  runtimeConfig: {
    externalIdentity: {
      clientId: '',
      clientSecret: '',
      tenantName: '',
      policy: '',
    },
    public: {
      apiBaseUrl: 'http://localhost:5000',
      demoMode: false,
      githubUrl: '',
      docsUrl: '',
    },
  },
})

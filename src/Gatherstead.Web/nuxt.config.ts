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
      { code: 'en', file: 'en.json', name: 'English' },
    ],
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

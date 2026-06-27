// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',

  ssr: process.env.NUXT_PUBLIC_DEMO_MODE !== 'true',

  vite: {
    define: {
      __DEMO_MODE__: JSON.stringify(process.env.NUXT_PUBLIC_DEMO_MODE === 'true'),
    },
  },

  devtools: { enabled: true },

  css: [
    '~/assets/css/main.css',
  ],

  modules: [
    '@nuxt/eslint',
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
    '/app/**': { ssr: false },
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
      redirectOn: 'all',
    },
    vueI18n: './i18n.config.ts',
  },

  runtimeConfig: {
    // Server-only (private) — Entra External ID, used for the OIDC authorization-code + PKCE flow in
    // server/routes/auth/azure.get.ts. No client secret: a public client + PKCE secures the exchange.
    // Bound from NUXT_EXTERNAL_IDENTITY_CLIENT_ID / _TENANT_NAME / _API_SCOPE.
    externalIdentity: {
      clientId: '',
      tenantName: '',
      // The API's exposed scope (e.g. api://<api-client-id>/access_as_user). Requested so the issued
      // access token's audience is the API; without it, API calls bearing the token 401.
      apiScope: '',
    },
    public: {
      apiBaseUrl: 'http://localhost:5000',
      demoMode: false,
      githubUrl: '',
      docsUrl: '',
      liveUrl: '',
      demoUrl: '',
      // Support contact address shown as a mailto link on the public /contact page. Bound from
      // NUXT_PUBLIC_CONTACT_EMAIL. Empty = the page shows a neutral fallback instead of a mailto link.
      contactEmail: '',
      // App Insights JS SDK connection string (browser telemetry). Bound from
      // NUXT_PUBLIC_APP_INSIGHTS_CONNECTION_STRING. Empty = telemetry disabled (local dev).
      appInsightsConnectionString: '',
    },
  },
})

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

  // Server-side store for the OAuth tokens (see server/utils/session.ts). The session cookie only
  // carries the user claims + an opaque session id, so the large Entra access/refresh tokens are kept
  // out of the cookie (they would blow past the browser's ~4096-byte cookie limit and get silently
  // dropped). In-memory driver: per-instance, lost on restart — the 401 interceptor re-authenticates
  // seamlessly under an active Entra SSO session. Swap `driver` for redis/azure-storage-table here to
  // persist across restarts.
  nitro: {
    storage: {
      sessions: { driver: 'memory' },
    },
  },

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
    'nuxt-security',
  ],

  typescript: {
    strict: true,
    typeCheck: 'build',
  },

  // Force dark theme app-wide. The light palette currently has poor contrast, so we lock the
  // color mode to dark and disable system detection (`preference: 'dark'` ignores the OS setting;
  // `fallback` is the belt-and-braces default). There is no in-app color-mode toggle, so this is
  // the single point of control. Revisit when the light theme is properly tuned.
  colorMode: {
    preference: 'dark',
    fallback: 'dark',
    // A previously stored preference in localStorage would otherwise win over `preference` above,
    // leaving existing visitors on light. Bumping the storage key ignores any stale value so dark
    // applies for everyone. Bump again if the enforcement ever needs another hard reset.
    storageKey: 'gs-color-mode-dark',
  },

  // Bundle used icons into the client at build time instead of fetching them from the Iconify
  // CDN (api.iconify.design) at runtime. The demo deploys as a static site (pnpm generate) with
  // no icon server endpoint, so without this every icon is fetched cross-origin — which App
  // Insights' traceparent correlation then trips over CORS. Scan finds the literal i-* names in
  // .vue sources; heroicons + lucide (@nuxt/ui defaults) collections are installed locally.
  icon: {
    clientBundle: {
      scan: true,
      sizeLimitKb: 256,
    },
  },

  routeRules: {
    '/': { prerender: true },
    // /terms and /privacy are deliberately left as default SSR (not prerendered) so their
    // config-driven legal identity (NUXT_PUBLIC_LEGAL_*) is read at runtime, mirroring /contact.
    '/tenants/**': { ssr: false },
    '/app/**': { ssr: false },
    '/user/**': { ssr: false },
    // Account moved out of tenant settings to its own user-scoped route.
    '/app/settings/account': { redirect: '/user/settings' },
  },

  // Content Security Policy (+ baseline security headers) via nuxt-security. Shipped report-only
  // first: verify zero violations live, then flip contentSecurityPolicyReportOnly to false.
  security: {
    contentSecurityPolicyReportOnly: true,

    // SSR routes: per-request nonce for inline Nuxt hydration scripts.
    nonce: true,

    // Prerendered `/` and the ssr:false SPA trees (/tenants/**, /app/**, and the whole app in
    // demo mode) have no per-request render, so their inline scripts are whitelisted by build-time
    // SHA-256 hash and the CSP is re-emitted as real HTTP headers through Nitro.
    ssg: {
      hashScripts: true,
      hashStyles: false, // styles via 'unsafe-inline'; hashing is brittle with Tailwind v4 / @nuxt/ui
      meta: false, // <meta> CSP can't carry report-only / frame-ancestors / report-uri
      nitroHeaders: true, // serve prerendered-page CSP as HTTP headers (default; explicit)
    },

    headers: {
      contentSecurityPolicy: {
        'base-uri': ["'self'"],
        'object-src': ["'none'"],
        'frame-ancestors': ["'none'"],
        // 'self' covers all same-origin _nuxt/*.js (incl. import-map-resolved #entry chunks) +
        // the npm-bundled App Insights SDK; nonce (SSR) + build-time hash (prerendered/SPA) cover
        // inline scripts. 'strict-dynamic' is deliberately absent: it discards 'self' in CSP3
        // browsers, which blocks the import-map-resolved entry module Nuxt/Vite emit.
        'script-src': ["'self'", "'nonce-{{nonce}}'"],
        // Vue / @nuxt/ui / Tailwind v4 inject runtime inline styles.
        'style-src': ["'self'", "'unsafe-inline'"],
        // Same-origin API via the Nitro proxy + App Insights ingestion + live metrics.
        'connect-src': [
          "'self'",
          'https://*.applicationinsights.azure.com',
          'https://*.in.applicationinsights.azure.com',
          'https://*.livediagnostics.monitor.azure.com',
        ],
        'font-src': ["'self'"], // @nuxt/fonts self-hosts at build
        'img-src': ["'self'", 'data:', 'https:'], // @nuxt/image + icon data URIs
        'form-action': ["'self'"],
        'upgrade-insecure-requests': true,
      },
    },
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
    // nuxt-auth-utils session cookie. It now carries only the user claims + the opaque session id
    // (the OAuth tokens live in the `sessions` server store), so we lock the cookie down explicitly.
    // `sameSite: 'lax'` (not 'strict') is required so the cookie survives the cross-site redirect back
    // from Entra. The `__Host-` prefix is the strongest binding (forces Secure + Path=/ + no Domain)
    // but breaks plain-http local dev, so it's production-only. Password is bound from
    // NUXT_SESSION_PASSWORD at runtime.
    session: {
      name: process.env.NODE_ENV === 'production' ? '__Host-session' : 'nuxt-session',
      cookie: {
        httpOnly: true,
        secure: true,
        sameSite: 'lax',
        path: '/',
      },
    },
    // Server-only (private) — Entra External ID, used for the OIDC authorization-code + PKCE flow in
    // server/routes/auth/azure.get.ts. The code is redeemed server-side as a confidential web client,
    // so a client secret is required (PKCE alone only works for browser/SPA cross-origin redemption).
    // Bound from NUXT_EXTERNAL_IDENTITY_CLIENT_ID / _CLIENT_SECRET / _TENANT_NAME / _API_SCOPE.
    externalIdentity: {
      clientId: '',
      // Client secret of the web app registration. In prod it's a Key Vault reference resolved by the
      // web app's managed identity (see infrastructure/modules/appservice.bicep).
      clientSecret: '',
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
      // Legal identity interpolated into /terms and /privacy at render. Kept out of source so a fork
      // of this open repo reflects its own operator, not us. Bound from NUXT_PUBLIC_LEGAL_PROVIDER /
      // NUXT_PUBLIC_LEGAL_JURISDICTION. Empty = neutral fallbacks (see app/content/legal/types.ts).
      legalProvider: '',
      legalJurisdiction: '',
      // App Insights JS SDK connection string (browser telemetry). Bound from
      // NUXT_PUBLIC_APP_INSIGHTS_CONNECTION_STRING. Empty = telemetry disabled (local dev).
      appInsightsConnectionString: '',
    },
  },
})

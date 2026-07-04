// Global $fetch interceptor for proxied API calls. The repository layer calls the global
// `$fetch('/api/proxy/...')` directly (no shared client), so wrapping the global is the single
// choke point for handling auth failures.
//
// When the server-side session can no longer mint a valid access token (the access token expired and
// the refresh token is gone/expired/revoked — see server/utils/auth.ts) the proxy returns 401. At that
// point the client still believes it is "logged in" (the session cookie's presence drives the flag),
// so we clear the client session and bounce to Entra sign-in. With an active Entra SSO session this is
// seamless; otherwise the user sees the Microsoft sign-in.
//
// Demo mode never hits the proxy, so the interceptor is a no-op there.
export default defineNuxtPlugin((nuxtApp) => {
  if (__DEMO_MODE__) return

  // Capture composables at setup time: the onResponseError callback runs outside Nuxt's injection
  // context, so resolving router/session/state there would throw.
  const router = useRouter()
  const { clear } = useUserSession()
  const loadingIndicator = useLoadingIndicator()
  // Shared reactive flag (also our re-entrancy guard): a page firing several proxied calls at once
  // would otherwise trigger one redirect per failed call. Components read this to show a re-auth hint.
  const reauthInFlight = useReauth()

  globalThis.$fetch = $fetch.create({
    onResponseError({ request, response }) {
      if (response.status !== 401) return
      if (!String(request).includes('/api/proxy/')) return
      if (reauthInFlight.value) return
      // Already on an auth route — don't loop.
      if (router.currentRoute.value.path.startsWith('/auth/')) return

      // Surface the silent re-auth: the shared flag drives the app-wide banner, and the loading
      // indicator gives the top progress bar (external nav bypasses the router's page:loading hooks).
      reauthInFlight.value = true
      loadingIndicator.start()
      // Reflect the dead session on the client, then re-authenticate. runWithContext restores the Nuxt
      // app context that navigateTo requires from inside this callback.
      clear().finally(() =>
        nuxtApp.runWithContext(() => navigateTo('/auth/azure', { external: true })),
      )
    },
  })
})

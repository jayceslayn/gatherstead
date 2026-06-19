import { watch } from 'vue'
import type { ApplicationInsights, ITelemetryItem } from '@microsoft/applicationinsights-web'
import { useTenantStore } from '~/stores/tenant'
import { setAnalytics } from '~/utils/telemetry'

// Browser telemetry via the Application Insights JS SDK. Cookieless (no consent banner)
// and PII-safe: only opaque IDs / enums / counts / short metadata are ever sent — never
// member names, emails, notes, etc. (see docs/OBSERVABILITY.md). No-op when no connection
// string is configured, so local dev stays clean.
//
// Stitching is hybrid (both cookieless):
//   - Demo → an in-memory session GUID, anonymous, grouping a visit within a tab.
//   - Prod → the in-memory session GUID on anonymous pages, upgraded to the authenticated
//     user identity (no cookie) once a signed-in user resolves.
export default defineNuxtPlugin(async (nuxtApp) => {
  const connectionString = useRuntimeConfig().public.appInsightsConnectionString
  if (!connectionString) return

  const { ApplicationInsights } = await import('@microsoft/applicationinsights-web')

  // One anonymous session identifier per page load — survives client-side route changes,
  // resets on full reload. The sole stitching mechanism for Demo; the fallback for Prod.
  const sessionId = crypto.randomUUID()
  const i18n = (nuxtApp as unknown as { $i18n?: { locale: { value: string } } }).$i18n

  const appInsights = new ApplicationInsights({
    config: {
      connectionString,
      disableCookiesUsage: true, // cookieless / no consent banner
      enableAutoRouteTracking: true, // SPA page views on vue-router navigation
      autoTrackPageVisitTime: true, // engagement: time-on-page
      enableCorsCorrelation: true, // Prod: correlate AJAX with backend traces
      distributedTracingMode: 2, // W3C — matches backend OTel
    },
  })
  appInsights.loadAppInsights()

  appInsights.addTelemetryInitializer((item: ITelemetryItem) => {
    item.tags = item.tags || {}
    item.tags['ai.cloud.role'] = __DEMO_MODE__ ? 'gatherstead-web-demo' : 'gatherstead-web'
    // Anonymous visit grouping. Overridden for authenticated Prod users by
    // setAuthenticatedUserContext below (which sets ai.user.authUserId).
    item.tags['ai.session.id'] = sessionId
    item.tags['ai.user.id'] = sessionId

    item.data = item.data || {}
    item.data.language = navigator.language
    item.data.locale = i18n?.locale.value ?? ''
    return true
  })

  nuxtApp.provide('appInsights', appInsights)
  // Make the instance reachable from the repository layer (outside Vue/Nuxt context).
  setAnalytics(appInsights)

  // Prod only: stitch telemetry to the signed-in user (opaque subject id) + tenant, without
  // a cookie. Demo stays purely anonymous on the in-memory session GUID.
  if (!__DEMO_MODE__) {
    const { user } = useAuth()
    const tenantStore = useTenantStore()
    watch(
      () => [(user.value as { id?: string } | null)?.id, tenantStore.currentTenantId] as const,
      ([userId, tenantId]) => {
        if (userId) {
          appInsights.setAuthenticatedUserContext(userId, tenantId ?? undefined, false)
        }
        else {
          appInsights.clearAuthenticatedUserContext()
        }
      },
      { immediate: true },
    )
  }
})

declare module '#app' {
  interface NuxtApp {
    // Undefined when no connection string is configured (local dev) — the plugin skips provide().
    $appInsights?: ApplicationInsights
  }
}

// PII-safe wrapper over the Application Insights JS SDK provided by analytics.client.ts.
// No-ops everywhere the SDK is absent (SSR, local dev with no connection string, or before
// the plugin runs), so callers never need to guard.
//
// The OBSERVABILITY.md rule applies to the frontend too: event names and property values
// (and the setUser arguments) must be internal IDs / enums / counts / short metadata only —
// NEVER member names, emails, notes, birth dates, or any other entity field value.
export function useAnalytics() {
  const appInsights = useNuxtApp().$appInsights

  return {
    trackEvent(name: string, properties?: Record<string, string | number>) {
      appInsights?.trackEvent({ name }, properties)
    },
    trackPageView(name?: string) {
      appInsights?.trackPageView(name ? { name } : undefined)
    },
    // Prod only: stitch telemetry to the signed-in user without a cookie.
    setUser(userId: string, tenantId?: string) {
      appInsights?.setAuthenticatedUserContext(userId, tenantId, false)
    },
    clearUser() {
      appInsights?.clearAuthenticatedUserContext()
    },
  }
}

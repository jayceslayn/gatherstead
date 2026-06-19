import type { ApplicationInsights } from '@microsoft/applicationinsights-web'

// Module-level analytics accessor for code that runs outside Vue/Nuxt context — the
// repository classes are plain objects (mirroring the module-level getDemoStore() singleton),
// so they cannot use useNuxtApp()/useAnalytics(). The analytics plugin registers the live
// instance here once initialized; everything no-ops until then (and forever in local dev,
// where no connection string is configured).
let appInsights: ApplicationInsights | null = null

export function setAnalytics(instance: ApplicationInsights | null) {
  appInsights = instance
}

export type PersistAction = 'create' | 'update' | 'delete' | 'set'

// Emits a `<entity>_<action>` custom event for a persisted write, in both Demo and Prod.
// PII-safe by contract (docs/OBSERVABILITY.md): entity + action are fixed enums and props
// must be counts / IDs / enums only — never member names, emails, notes, etc.
export function trackPersistence(
  entity: string,
  action: PersistAction,
  props?: Record<string, string | number>,
) {
  appInsights?.trackEvent({ name: `${entity}_${action}` }, props)
}

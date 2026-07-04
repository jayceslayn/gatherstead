// Shared reactive flag, set by the 401 interceptor (plugins/api.client.ts) while a silent re-auth
// redirect is in flight so the UI can surface a "signing you back in" hint instead of a terminal error.
// The re-auth path does a full external navigation, so the app reloads on the way back and this flag
// resets to false naturally — no manual teardown needed.
export function useReauth() {
  return useState<boolean>('auth:reauthInFlight', () => false)
}

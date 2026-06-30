import type { H3Event } from 'h3'

export interface SecureSession {
  accessToken: string
  refreshToken?: string
  // Epoch ms at which the access token expires (derived from the token response's expires_in).
  expiresAt?: number
  // Epoch ms after which this stored entry may be culled. Re-stamped on every persist (login + each
  // refresh), so it is an *inactivity* bound rather than a hard session lifetime — an active session
  // never reaches it.
  refreshExpiresAt?: number
}

// How long a stored secure session survives without being refreshed before it is culled. Because it is
// re-stamped on every persist, only abandoned sessions hit it. An evicted-but-otherwise-valid session
// simply re-authenticates silently via the client 401 interceptor under an active Entra SSO session.
const REFRESH_TOKEN_TTL_MS = 7 * 24 * 60 * 60 * 1000 // 7 days

// How often the server-side sweep (server/plugins/session-sweep.ts) culls expired entries.
export const SWEEP_INTERVAL_MS = 15 * 60 * 1000 // 15 minutes

// The OAuth tokens are kept server-side keyed by the nuxt-auth-utils session id instead of in the
// session cookie: both Entra tokens together blow past the browser's ~4096-byte cookie limit and the
// cookie gets silently dropped (nuxt-auth-utils stores the whole session in one sealed cookie). This is
// the BFF pattern recommended by the library maintainer (atinux/nuxt-auth-utils#354) — the cookie
// carries only the user claims + the opaque session id; the tokens never reach the browser. The mount
// is configured in nuxt.config.ts (`nitro.storage.sessions`).
function sessionStore() {
  return useStorage<SecureSession>('sessions')
}

// The nuxt-auth-utils/h3 session id sealed into the cookie. Stable for an authenticated request; a
// fresh (unmatched) id is generated for requests without a session cookie, which simply misses the
// store and reads as "no secure session".
async function getSessionId(event: H3Event): Promise<string> {
  const session = await getUserSession(event)
  return session.id
}

export async function getSecureSession(event: H3Event): Promise<SecureSession | undefined> {
  const id = await getSessionId(event)
  if (!id) {
    return undefined
  }
  const secure = await sessionStore().getItem(id)
  if (!secure) {
    return undefined
  }
  // Lazy eviction: drop entries that have outlived their refresh window.
  if (typeof secure.refreshExpiresAt === 'number' && Date.now() >= secure.refreshExpiresAt) {
    await sessionStore().removeItem(id)
    return undefined
  }
  return secure
}

// Writes the tokens to the server store under the current session id. Requires a session to already
// exist (setUserSession at login creates it); on an event with no session a fresh id is generated and
// the entry would be unreachable, so only call this after the session is established.
export async function persistSecureSession(event: H3Event, secure: SecureSession): Promise<void> {
  const id = await getSessionId(event)
  await sessionStore().setItem(id, secure)
}

// Removes both the server-side tokens and the client session cookie.
export async function clearSecureSession(event: H3Event): Promise<void> {
  const id = await getSessionId(event)
  if (id) {
    await sessionStore().removeItem(id)
  }
  await clearUserSession(event)
}

export function buildSecureSession(
  accessToken: string,
  refreshToken?: string,
  expiresIn?: number,
): SecureSession {
  const now = Date.now()
  const expiresAt = typeof expiresIn === 'number' ? now + expiresIn * 1000 : undefined
  return {
    accessToken,
    refreshToken,
    expiresAt,
    // With a refresh token the entry stays useful well beyond the access-token expiry; without one it
    // is only good until the access token expires.
    refreshExpiresAt: refreshToken ? now + REFRESH_TOKEN_TTL_MS : expiresAt,
  }
}

// Culls every stored entry past its refresh window. Bounds memory (and stale-secret lifetime) for
// sessions that are abandoned and never read again, complementing the lazy eviction in getSecureSession.
export async function sweepExpiredSessions(): Promise<void> {
  const store = sessionStore()
  const now = Date.now()
  for (const id of await store.getKeys()) {
    const secure = await store.getItem(id)
    if (secure && typeof secure.refreshExpiresAt === 'number' && now >= secure.refreshExpiresAt) {
      await store.removeItem(id)
    }
  }
}

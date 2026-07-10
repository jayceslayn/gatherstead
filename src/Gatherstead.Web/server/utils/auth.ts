import type { H3Event } from 'h3'
import { buildSecureSession, clearSecureSession, getSecureSession, persistSecureSession } from '~~/server/utils/session'

interface OidcTokenResponse {
  access_token: string
  refresh_token?: string
  expires_in?: number
}

// Entra External ID (CIAM) authority for the tenant. Shared by the login handler
// (server/routes/auth/azure.get.ts) and the refresh-token flow below.
export function buildAuthority(tenantName: string): string {
  return `https://${tenantName}.ciamlogin.com/${tenantName}.onmicrosoft.com`
}

// Entra External ID end-session (federated sign-out) endpoint. Hitting it terminates the IdP's SSO
// cookie so the *next* /auth/azure login is a full sign-in (or "Create one" sign-up) instead of a silent
// SSO re-auth. Used only by the explicit logout route (server/routes/auth/logout.get.ts) — the silent
// re-auth path deliberately does not, so a timed-out session resumes without an account picker.
// `postLogoutRedirectUri` must exactly match a redirect URI registered on the web app registration, or
// Entra ignores it and strands the user on its own signed-out page (see docs/DEPLOYMENT.md).
export function buildLogoutUrl(tenantName: string, postLogoutRedirectUri: string): string {
  const query = new URLSearchParams({ post_logout_redirect_uri: postLogoutRedirectUri })
  return `${buildAuthority(tenantName)}/oauth2/v2.0/logout?${query}`
}

// Refresh slightly before the real expiry so an in-flight request never carries a token that
// expires mid-flight (independent of the API's own clock skew).
const EXPIRY_SKEW_MS = 60_000

// OAuth 2.0 token-endpoint error codes (RFC 6749 §5.2). Used to constrain what we log from the
// token endpoint's error body to a known, non-sensitive set (`invalid_grant` is handled above).
const KNOWN_OAUTH_ERRORS = new Set([
  'invalid_request',
  'invalid_client',
  'invalid_grant',
  'unauthorized_client',
  'unsupported_grant_type',
  'invalid_scope',
])

// Coalesce concurrent refreshes: the /tenants landing fires several proxied calls at once and Entra
// rotates the refresh token on each use, so parallel refreshes would invalidate one another. This
// dedups in-flight refreshes keyed by the refresh token — per app instance, which is sufficient for
// the single Web App instance we run.
const inFlightRefreshes = new Map<string, Promise<OidcTokenResponse>>()

function refreshTokens(refreshToken: string): Promise<OidcTokenResponse> {
  const existing = inFlightRefreshes.get(refreshToken)
  if (existing) {
    return existing
  }

  const { clientId, clientSecret, tenantName, apiScope } = useRuntimeConfig().externalIdentity
  // Same scope set requested at login (server/routes/auth/azure.get.ts) so the new access token keeps
  // the API audience and we continue to receive a rotated refresh token via offline_access.
  const scope = ['openid', 'profile', 'email', 'offline_access', apiScope].filter(Boolean).join(' ')

  const promise = $fetch<OidcTokenResponse>(`${buildAuthority(tenantName)}/oauth2/v2.0/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'refresh_token',
      client_id: clientId,
      client_secret: clientSecret,
      refresh_token: refreshToken,
      scope,
    }),
  }).finally(() => inFlightRefreshes.delete(refreshToken))

  inFlightRefreshes.set(refreshToken, promise)
  return promise
}

// Returns a non-expired access token for the proxied API call, transparently renewing it with the
// refresh token when it is at/near expiry. When renewal is impossible (no refresh token, or the
// refresh token is itself expired/revoked) the session is cleared and a 401 is thrown so the client
// re-authenticates.
export async function getValidAccessToken(event: H3Event): Promise<string> {
  const secure = await getSecureSession(event)
  if (!secure?.accessToken) {
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized' })
  }

  const expired = typeof secure.expiresAt === 'number'
    && Date.now() >= secure.expiresAt - EXPIRY_SKEW_MS
  if (!expired) {
    return secure.accessToken
  }

  if (!secure.refreshToken) {
    await clearSecureSession(event)
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized' })
  }

  try {
    const tokens = await refreshTokens(secure.refreshToken)
    // Tokens live in the server-side store (keyed by session id), not the cookie, so the rotated
    // refresh token is written there and is unaffected by the downstream proxyRequest's Set-Cookie
    // handling. Entra rotates the refresh token on each use; fall back to the existing one if absent.
    await persistSecureSession(event, buildSecureSession(
      tokens.access_token,
      tokens.refresh_token ?? secure.refreshToken,
      tokens.expires_in,
    ))
    return tokens.access_token
  }
  catch (err) {
    // The token endpoint returns 400 { error: "invalid_grant" } when the refresh token is itself
    // expired/revoked (RFC 6749 §5.2) — the one case where re-auth is the fix. Match on that error
    // code (ofetch parses the JSON body onto err.data), not the 400 status alone: other 400s
    // (invalid_request / invalid_scope / invalid_client) are config faults where clearing the session
    // would only cause a re-auth loop. Anything else is treated as transient.
    const oauthError = (err as { data?: { error?: string } }).data?.error
    if (oauthError === 'invalid_grant') {
      // Clear the session so the client 401 interceptor re-authenticates (seamless under an active
      // Entra SSO session).
      console.error('Azure refresh token rejected (invalid_grant); clearing session')
      await clearSecureSession(event)
      throw createError({ statusCode: 401, statusMessage: 'Unauthorized' })
    }
    // Transient (network / 5xx / timeout) or a config fault: keep the session so the next request
    // retries the refresh. 503 (not 401) avoids tripping the client's re-auth interceptor into a
    // re-auth storm, and surfaces config faults in logs rather than as a silent logout loop.
    const status = (err as { status?: number }).status
    // Only log a recognised OAuth error code (or a fixed placeholder), never raw response content.
    const safeOauthError = KNOWN_OAUTH_ERRORS.has(oauthError as string) ? oauthError : 'unrecognized'
    console.error('Azure token refresh failed; session retained', { status, oauthError: safeOauthError })
    throw createError({ statusCode: 503, statusMessage: 'Service Unavailable' })
  }
}

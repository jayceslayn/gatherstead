import { buildAuthority } from '~~/server/utils/auth'
import { buildSecureSession, persistSecureSession } from '~~/server/utils/session'

// Microsoft Entra External ID (ciamlogin.com) sign-in. The Nuxt server redeems the authorization code
// server-side, so this is a confidential client: the redirect URI is registered under a "Web" platform
// and the code is exchanged with a client secret. nuxt-auth-utils' generic `oidc` provider does the full
// standards flow for us — state (CSRF) + PKCE + nonce + secret-bearing code exchange.
const { clientId, clientSecret, tenantName, apiScope } = useRuntimeConfig().externalIdentity
const authority = buildAuthority(tenantName)

export default defineOAuthOidcEventHandler({
  config: {
    clientId,
    clientSecret,
    // Inline endpoints with NO userinfo_endpoint on purpose: the access token is audienced for our API
    // (via apiScope), so it can't be used against Graph's userinfo endpoint. We take identity from the
    // id_token instead and keep the access token for API calls (see getValidAccessToken in server/utils/auth.ts).
    openidConfig: {
      authorization_endpoint: `${authority}/oauth2/v2.0/authorize`,
      token_endpoint: `${authority}/oauth2/v2.0/token`,
    },
    // openid/profile/email for the id_token claims; offline_access for refresh; apiScope so the access
    // token is audienced for the Gatherstead API.
    scope: ['openid', 'profile', 'email', 'offline_access', apiScope].filter(Boolean),
  },
  async onSuccess(event, { tokens }) {
    // The id_token came directly from the token endpoint over TLS and the provider already validated its
    // nonce, so decode the payload for the identity claims without re-verifying the signature.
    const payload = tokens.id_token?.split('.')[1]
    if (!payload) {
      console.error('Azure OIDC error: token response had no/malformed id_token')
      return sendRedirect(event, '/')
    }
    const claims = JSON.parse(
      Buffer.from(payload, 'base64url').toString(),
    ) as { sub: string, name?: string, email?: string, preferred_username?: string }
    // Establish the session (and its id + small cookie) with only the user claims, then stash the
    // tokens in the server-side store keyed by that session id — keeping the large Entra tokens out of
    // the cookie. setUserSession must run first so persistSecureSession has a session id to key on.
    await setUserSession(event, {
      user: {
        id: claims.sub,
        name: claims.name ?? claims.preferred_username ?? '',
        email: claims.email ?? claims.preferred_username ?? '',
      },
    })
    await persistSecureSession(
      event,
      buildSecureSession(tokens.access_token, tokens.refresh_token, tokens.expires_in),
    )

    // Provision the internal Users row (and claim any pending invitations) once, at login, so the
    // first authenticated call (GET /api/proxy/tenants on /tenants) resolves to a real user instead
    // of 401 "Authentication required." Best-effort: a failure is logged but must not block sign-in
    // — the /app route middleware (ensureBootstrap) remains a fallback.
    const config = useRuntimeConfig(event)
    if (tokens.access_token) {
      try {
        await $fetch(`${config.public.apiBaseUrl}/api/me/bootstrap`, {
          method: 'POST',
          headers: { Authorization: `Bearer ${tokens.access_token}` },
        })
      }
      catch (err) {
        console.error('Azure OIDC: user bootstrap failed', err)
      }
    }

    return sendRedirect(event, '/tenants')
  },
  onError(event, error) {
    console.error('Azure OIDC error:', error)
    return sendRedirect(event, '/')
  },
})

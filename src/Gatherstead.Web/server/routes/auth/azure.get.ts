import { buildSecureSession } from '~~/server/utils/session'

// Microsoft Entra External ID (ciamlogin.com) sign-in. The Nuxt server redeems the authorization code
// server-side, so this is a confidential client: the redirect URI is registered under a "Web" platform
// and the code is exchanged with a client secret. nuxt-auth-utils' generic `oidc` provider does the full
// standards flow for us — state (CSRF) + PKCE + nonce + secret-bearing code exchange.
const { clientId, clientSecret, tenantName, apiScope } = useRuntimeConfig().externalIdentity
const authority = `https://${tenantName}.ciamlogin.com/${tenantName}.onmicrosoft.com`

export default defineOAuthOidcEventHandler({
  config: {
    clientId,
    clientSecret,
    // Inline endpoints with NO userinfo_endpoint on purpose: the access token is audienced for our API
    // (via apiScope), so it can't be used against Graph's userinfo endpoint. We take identity from the
    // id_token instead and keep the access token for API calls (see getAccessToken in server/utils/session.ts).
    openidConfig: {
      authorization_endpoint: `${authority}/oauth2/v2.0/authorize`,
      token_endpoint: `${authority}/oauth2/v2.0/token`,
    },
    // openid/profile/email for the id_token claims; offline_access for refresh; apiScope so the access
    // token is audienced for the Gatherstead API.
    scope: ['openid', 'profile', 'email', 'offline_access', apiScope].filter(Boolean),
  },
  async onSuccess(event, { tokens }) {
    if (!tokens.id_token) {
      console.error('Azure OIDC error: token response contained no id_token')
      return sendRedirect(event, '/')
    }
    // The id_token came directly from the token endpoint over TLS and the provider already validated its
    // nonce, so decode the payload for the identity claims without re-verifying the signature.
    const claims = JSON.parse(
      Buffer.from(tokens.id_token.split('.')[1], 'base64url').toString(),
    ) as { sub: string, name?: string, email?: string, preferred_username?: string }
    await setUserSession(event, {
      user: {
        id: claims.sub,
        name: claims.name ?? claims.preferred_username ?? '',
        email: claims.email ?? claims.preferred_username ?? '',
      },
      secure: buildSecureSession(tokens.access_token),
    })
    return sendRedirect(event, '/tenants')
  },
  onError(event, error) {
    console.error('Azure OIDC error:', error)
    return sendRedirect(event, '/')
  },
})

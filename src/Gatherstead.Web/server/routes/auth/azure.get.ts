import { buildSecureSession } from '~~/server/utils/session'

// nuxt-auth-utils ships only an Azure AD B2C provider, whose defaults target b2clogin.com and read
// runtimeConfig.oauth.azureb2c. We point it at Microsoft Entra External ID (ciamlogin.com) instead,
// feeding config from the private runtimeConfig.externalIdentity section and overriding the endpoints.
const { clientId, tenantName, apiScope } = useRuntimeConfig().externalIdentity
const authority = `https://${tenantName}.ciamlogin.com/${tenantName}.onmicrosoft.com`

export default defineOAuthAzureB2CEventHandler({
  config: {
    clientId,
    tenant: tenantName,
    authorizationURL: `${authority}/oauth2/v2.0/authorize`,
    tokenURL: `${authority}/oauth2/v2.0/token`,
    // OIDC userinfo endpoint — returns sub/name/email claims (Graph /v1.0/me, the provider default,
    // returns id/mail instead and would break the claim mapping below).
    userURL: 'https://graph.microsoft.com/oidc/userinfo',
    // openid/profile/email for the user claims; offline_access for refresh; apiScope so the access
    // token is audienced for the Gatherstead API (see getAccessToken in server/utils/session.ts).
    scope: ['openid', 'profile', 'email', 'offline_access', apiScope].filter(Boolean),
  },
  async onSuccess(event, { user, tokens }) {
    await setUserSession(event, {
      user: {
        id: user.sub,
        name: user.name || user.preferred_username,
        email: user.email || user.preferred_username,
      },
      secure: buildSecureSession(tokens.access_token),
    })
    return sendRedirect(event, '/tenants')
  },
  onError(event, error) {
    console.error('Azure OAuth error:', error)
    return sendRedirect(event, '/')
  },
})

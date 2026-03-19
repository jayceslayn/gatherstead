import { buildSecureSession } from '~~/server/utils/session'

export default defineOAuthAzureB2CEventHandler({
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

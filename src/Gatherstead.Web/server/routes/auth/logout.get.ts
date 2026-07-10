import { buildLogoutUrl } from '~~/server/utils/auth'
import { clearSecureSession } from '~~/server/utils/session'

// Explicit sign-out. Clears Gatherstead's own session (server-side tokens + cookie), then performs a
// *federated* sign-out at Entra External ID so the IdP's SSO cookie is torn down too. Without the
// federated step the next /auth/azure login would silently ride the still-active SSO session back in as
// the same account — so the user could never switch accounts or reach the "Create one" sign-up flow.
//
// This is deliberately asymmetric with the silent re-auth path (app/plugins/api.client.ts): a timed-out
// session recovers by hitting /auth/azure with no prompt and resumes without an account picker. Only
// this explicit action tears the SSO session down.
export default defineEventHandler(async (event) => {
  await clearSecureSession(event)

  const { tenantName } = useRuntimeConfig(event).externalIdentity
  // Post-logout landing = the prerendered "/", derived from the request so it matches the origin Entra
  // saw at login. Must be registered as a redirect URI on the web app registration (see DEPLOYMENT.md).
  const postLogoutRedirectUri = `${getRequestURL(event).origin}/`
  return sendRedirect(event, buildLogoutUrl(tenantName, postLogoutRedirectUri))
})

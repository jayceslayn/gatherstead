import { clearSecureSession } from '~~/server/utils/session'

export default defineEventHandler(async (event) => {
  // Clears both the server-side tokens and the session cookie.
  await clearSecureSession(event)
  return sendRedirect(event, '/')
})

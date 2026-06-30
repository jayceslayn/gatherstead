import { SWEEP_INTERVAL_MS, sweepExpiredSessions } from '~~/server/utils/session'

// Periodically culls expired entries from the server-side secure-session store so abandoned sessions
// don't retain refresh tokens (or grow memory) indefinitely — the active counterpart to the lazy
// eviction in getSecureSession. `unref()` keeps this timer from holding the process open on shutdown.
export default defineNitroPlugin(() => {
  const timer = setInterval(() => {
    sweepExpiredSessions().catch(err => console.error('Secure-session sweep failed', err))
  }, SWEEP_INTERVAL_MS)
  timer.unref?.()
})

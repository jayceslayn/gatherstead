import { describe, expect, it, vi, afterEach } from 'vitest'
import { buildSecureSession } from '~~/server/utils/session'

// buildSecureSession is a pure function (only Date.now + arithmetic), so it is unit-testable without
// any Nuxt runtime. It encodes the security-relevant rule for how long server-side OAuth tokens are
// retained: with a refresh token the entry lives for the 7-day inactivity window; without one it is
// only good until the access token itself expires (so an abandoned, unrefreshable session is culled
// promptly rather than lingering as a stale secret).
const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000

describe('buildSecureSession', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('derives expiresAt from expiresIn seconds', () => {
    vi.useFakeTimers()
    const now = new Date('2026-01-01T00:00:00.000Z').getTime()
    vi.setSystemTime(now)

    const session = buildSecureSession('access', 'refresh', 3600)

    expect(session.accessToken).toBe('access')
    expect(session.refreshToken).toBe('refresh')
    expect(session.expiresAt).toBe(now + 3600 * 1000)
  })

  it('with a refresh token, refreshExpiresAt is the 7-day inactivity window', () => {
    vi.useFakeTimers()
    const now = Date.now()
    vi.setSystemTime(now)

    const session = buildSecureSession('access', 'refresh', 3600)

    expect(session.refreshExpiresAt).toBe(now + SEVEN_DAYS_MS)
  })

  it('without a refresh token, refreshExpiresAt collapses to the access-token expiry', () => {
    vi.useFakeTimers()
    const now = Date.now()
    vi.setSystemTime(now)

    const session = buildSecureSession('access', undefined, 3600)

    expect(session.refreshToken).toBeUndefined()
    expect(session.refreshExpiresAt).toBe(now + 3600 * 1000)
    expect(session.refreshExpiresAt).toBe(session.expiresAt)
  })

  it('leaves expiry fields undefined when expiresIn is absent', () => {
    const session = buildSecureSession('access', 'refresh')

    expect(session.expiresAt).toBeUndefined()
    // Without an access-token expiry but with a refresh token, the inactivity window still applies.
    expect(typeof session.refreshExpiresAt).toBe('number')
  })
})

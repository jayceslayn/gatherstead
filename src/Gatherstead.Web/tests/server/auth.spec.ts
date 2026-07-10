import { beforeEach, describe, expect, it, vi } from 'vitest'

// ── Mocks for the Nuxt/h3 dependencies auth.ts reaches at call time ─────────────────────────────
// vi.hoisted lets the (hoisted) vi.mock factory reference these without a temporal-dead-zone error.
const mocks = vi.hoisted(() => ({
  getSecureSession: vi.fn(),
  clearSecureSession: vi.fn(),
  persistSecureSession: vi.fn(),
}))

vi.mock('~~/server/utils/session', () => ({
  getSecureSession: mocks.getSecureSession,
  clearSecureSession: mocks.clearSecureSession,
  persistSecureSession: mocks.persistSecureSession,
  // Pass-through so the persisted value is inspectable in the refresh test.
  buildSecureSession: (accessToken: string, refreshToken?: string, expiresIn?: number) => ({
    accessToken,
    refreshToken,
    expiresIn,
  }),
}))

const $fetch = vi.fn()

// h3's createError: build a throwable carrying statusCode, matching how the real one is consumed.
function createError(opts: { statusCode: number, statusMessage?: string }) {
  const e = new Error(opts.statusMessage ?? 'error') as Error & { statusCode: number }
  e.statusCode = opts.statusCode
  return e
}

const useRuntimeConfig = () => ({
  externalIdentity: {
    clientId: 'client-id',
    clientSecret: 'client-secret',
    tenantName: 'contoso',
    apiScope: 'api://gatherstead/.default',
  },
})

vi.stubGlobal('$fetch', $fetch)
vi.stubGlobal('createError', createError)
vi.stubGlobal('useRuntimeConfig', useRuntimeConfig)

const { buildAuthority, buildLogoutUrl, getValidAccessToken } = await import('~~/server/utils/auth')

// The event object is only forwarded to the mocked session helpers, so a bare cast suffices.
const event = {} as never

describe('buildAuthority', () => {
  it('builds the CIAM authority URL for a tenant', () => {
    expect(buildAuthority('contoso')).toBe('https://contoso.ciamlogin.com/contoso.onmicrosoft.com')
  })
})

describe('buildLogoutUrl', () => {
  it('builds the CIAM end-session URL with an encoded post-logout redirect', () => {
    expect(buildLogoutUrl('contoso', 'https://app.example.com/')).toBe(
      'https://contoso.ciamlogin.com/contoso.onmicrosoft.com/oauth2/v2.0/logout'
      + '?post_logout_redirect_uri=https%3A%2F%2Fapp.example.com%2F',
    )
  })
})

describe('getValidAccessToken', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('returns the current token when it is not near expiry', async () => {
    mocks.getSecureSession.mockResolvedValue({ accessToken: 'live-token', expiresAt: Date.now() + 10 * 60_000 })

    await expect(getValidAccessToken(event)).resolves.toBe('live-token')
    expect($fetch).not.toHaveBeenCalled()
    expect(mocks.persistSecureSession).not.toHaveBeenCalled()
  })

  it('throws 401 when there is no session', async () => {
    mocks.getSecureSession.mockResolvedValue(undefined)

    await expect(getValidAccessToken(event)).rejects.toMatchObject({ statusCode: 401 })
  })

  it('clears the session and throws 401 when expired with no refresh token', async () => {
    mocks.getSecureSession.mockResolvedValue({ accessToken: 'old', expiresAt: Date.now() - 1, refreshToken: undefined })

    await expect(getValidAccessToken(event)).rejects.toMatchObject({ statusCode: 401 })
    expect(mocks.clearSecureSession).toHaveBeenCalledOnce()
  })

  it('refreshes and persists the rotated token when expired with a refresh token', async () => {
    mocks.getSecureSession.mockResolvedValue({ accessToken: 'old', expiresAt: Date.now() - 1, refreshToken: 'rt-1' })
    $fetch.mockResolvedValue({ access_token: 'new-token', refresh_token: 'rt-2', expires_in: 3600 })

    await expect(getValidAccessToken(event)).resolves.toBe('new-token')
    expect(mocks.persistSecureSession).toHaveBeenCalledOnce()
    const persisted = mocks.persistSecureSession.mock.calls[0][1]
    expect(persisted).toMatchObject({ accessToken: 'new-token', refreshToken: 'rt-2' })
  })

  it('clears the session and throws 401 on invalid_grant (refresh token revoked/expired)', async () => {
    mocks.getSecureSession.mockResolvedValue({ accessToken: 'old', expiresAt: Date.now() - 1, refreshToken: 'rt-1' })
    $fetch.mockRejectedValue(Object.assign(new Error('bad'), { status: 400, data: { error: 'invalid_grant' } }))

    await expect(getValidAccessToken(event)).rejects.toMatchObject({ statusCode: 401 })
    expect(mocks.clearSecureSession).toHaveBeenCalledOnce()
  })

  it('retains the session and throws 503 on a transient refresh failure', async () => {
    mocks.getSecureSession.mockResolvedValue({ accessToken: 'old', expiresAt: Date.now() - 1, refreshToken: 'rt-1' })
    $fetch.mockRejectedValue(Object.assign(new Error('boom'), { status: 500 }))

    await expect(getValidAccessToken(event)).rejects.toMatchObject({ statusCode: 503 })
    expect(mocks.clearSecureSession).not.toHaveBeenCalled()
  })
})

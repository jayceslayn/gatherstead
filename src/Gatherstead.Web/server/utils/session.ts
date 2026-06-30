import type { H3Event } from 'h3'

export interface SecureSession {
  accessToken: string
  refreshToken?: string
  // Epoch ms at which the access token expires (derived from the token response's expires_in).
  expiresAt?: number
}

export async function getSecureSession(event: H3Event): Promise<SecureSession | undefined> {
  const session = await getUserSession(event)
  return session?.secure as unknown as SecureSession | undefined
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any -- #auth-utils module augmentation not resolved during vue-tsc build
export function buildSecureSession(accessToken: string, refreshToken?: string, expiresIn?: number): any {
  return {
    accessToken,
    refreshToken,
    expiresAt: typeof expiresIn === 'number' ? Date.now() + expiresIn * 1000 : undefined,
  }
}

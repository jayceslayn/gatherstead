import type { H3Event } from 'h3'

interface SecureSession {
  accessToken: string
}

export async function getAccessToken(event: H3Event): Promise<string> {
  const session = await getUserSession(event)
  const secure = session?.secure as unknown as SecureSession | undefined

  if (!secure?.accessToken) {
    throw createError({ statusCode: 401, statusMessage: 'Unauthorized' })
  }

  return secure.accessToken
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any -- #auth-utils module augmentation not resolved during vue-tsc build
export function buildSecureSession(accessToken: string): any {
  return { accessToken }
}

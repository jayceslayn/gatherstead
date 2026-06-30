import { getValidAccessToken } from '~~/server/utils/auth'

export default defineEventHandler(async (event) => {
  const accessToken = await getValidAccessToken(event)

  const config = useRuntimeConfig()
  const path = getRouterParam(event, 'path') || ''
  const targetUrl = `${config.public.apiBaseUrl}/api/${path}`

  return proxyRequest(event, targetUrl, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  })
})

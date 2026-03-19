import { getAccessToken } from '~~/server/utils/session'

export default defineEventHandler(async (event) => {
  const accessToken = await getAccessToken(event)

  const config = useRuntimeConfig()
  const path = getRouterParam(event, 'path') || ''
  const targetUrl = `${config.public.apiBaseUrl}/api/${path}`

  return proxyRequest(event, targetUrl, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  })
})

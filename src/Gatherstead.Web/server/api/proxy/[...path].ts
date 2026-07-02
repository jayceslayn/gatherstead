import { getValidAccessToken } from '~~/server/utils/auth'

export default defineEventHandler(async (event) => {
  const accessToken = await getValidAccessToken(event)

  const config = useRuntimeConfig()
  const path = getRouterParam(event, 'path') || ''
  // getRouterParam returns only the matched path segments, so the incoming query
  // string must be re-appended — proxyRequest fetches the target URL verbatim.
  const targetUrl = `${config.public.apiBaseUrl}/api/${path}${getRequestURL(event).search}`

  return proxyRequest(event, targetUrl, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  })
})

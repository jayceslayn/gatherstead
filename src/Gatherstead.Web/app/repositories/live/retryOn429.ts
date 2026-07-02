import { FetchError } from 'ofetch'

/**
 * Runs a $fetch call, retrying on HTTP 429 while honouring the server's `Retry-After` header.
 * Rate limiting should be rare now that sign-up flows batch their writes; this is a defensive
 * backstop so a brief burst backs off and succeeds instead of surfacing an error. The wait is
 * bounded — if the server asks us to wait longer than `maxWaitMs`, we give up and let the error
 * propagate rather than freezing the UI for a full rate-limit window.
 */
export async function retryOn429<T>(fn: () => Promise<T>, maxRetries = 2, maxWaitMs = 10_000): Promise<T> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await fn()
    }
    catch (e) {
      if (!(e instanceof FetchError) || e.statusCode !== 429 || attempt >= maxRetries) throw e

      const retryAfter = e.response?.headers.get('retry-after')
      const retryAfterMs = retryAfter ? Number.parseInt(retryAfter, 10) * 1000 : 1000 * (attempt + 1)
      if (!Number.isFinite(retryAfterMs) || retryAfterMs > maxWaitMs) throw e

      await new Promise(resolve => setTimeout(resolve, retryAfterMs))
    }
  }
}

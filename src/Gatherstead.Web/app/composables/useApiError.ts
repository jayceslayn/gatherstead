interface ResponseMessage {
  type?: string
  code?: string
  message?: string
  params?: Record<string, string>
}

interface ApiErrorShape {
  code?: string
  detail?: string
  message?: string
  messages?: ResponseMessage[]
  errors?: Record<string, string[]>
  data?: ApiErrorShape
}

export function useApiError() {
  const { t, te } = useI18n()

  // The reserved `entity` param is a stable token (e.g. "accommodation"); resolve it through i18n so
  // entity nouns localize, falling back to the literal token when no translation exists. All other
  // params are literal interpolation values passed straight through.
  function resolveParams(params?: Record<string, string>): Record<string, string> {
    if (!params) return {}
    const resolved: Record<string, string> = { ...params }
    if (params.entity !== undefined) {
      const key = `apiError.entity.${params.entity}`
      resolved.entity = te(key) ? t(key) : params.entity
    }
    return resolved
  }

  function translateError(e: unknown): string {
    // $fetch (ofetch) errors wrap the parsed API body in .data; plain objects are used directly.
    const err = e as ApiErrorShape
    const data = err?.data ?? err
    const msg = data?.messages?.find(m => m?.type === 'ERROR')

    // 1. Stable code -> localized template (interpolating params). Falls through when no template exists.
    if (msg?.code) {
      const key = `apiError.${msg.code}`
      if (te(key)) return t(key, resolveParams(msg.params))
    }
    // 2. Server-provided human-readable message (English fallback).
    if (msg?.message) return msg.message

    // 3. ASP.NET model validation (ValidationProblemDetails carries an `errors` dictionary).
    const validation = data?.errors ? Object.values(data.errors).flat().filter(Boolean) : []
    if (validation.length) return validation.join(' ')

    // 4. RFC7807 ProblemDetails.
    if (data?.detail) return data.detail

    // 5. Nothing parseable — never surface ofetch's "[POST] <route>: 400 Bad Request" string.
    return t('error.serverError')
  }

  return { translateError }
}

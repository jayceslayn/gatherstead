interface ApiErrorShape {
  code?: string
  detail?: string
  message?: string
  data?: ApiErrorShape
}

export function useApiError() {
  const { t, te } = useI18n()

  function translateError(e: unknown): string {
    // $fetch errors wrap the API response body in .data; plain objects are used directly
    const err = e as ApiErrorShape
    const data = err?.data ?? err
    const code = data?.code ?? ''
    const detail = data?.detail ?? err?.message
    const key = `apiError.${code}`
    return te(key) ? t(key) : detail ?? t('error.serverError')
  }

  return { translateError }
}

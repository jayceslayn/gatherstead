export function useApiError() {
  const { t, te } = useI18n()

  function translateError(apiError: { code: string; detail?: string }): string {
    const key = `apiError.${apiError.code}`
    return te(key) ? t(key) : apiError.detail ?? t('error.serverError')
  }

  return { translateError }
}

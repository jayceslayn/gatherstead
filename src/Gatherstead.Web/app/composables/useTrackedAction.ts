import { DemoLimitError } from '~/repositories/interfaces'

/**
 * Shared wrapper for repository mutations. `run` tracks the in-flight key on `updating`
 * (entity id, or 'new' for creates), refreshes on success, and surfaces failures as
 * toasts — DemoLimitError as the demo-limit warning, everything else through
 * translateError — resolving true on success, false on failure.
 *
 * Pass `refresh` when every action should re-fetch after a successful mutation; omit it
 * when the caller updates local state inside `fn` instead.
 */
export function useTrackedAction(refresh?: () => Promise<void>) {
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function run(key: string, fn: () => Promise<unknown>): Promise<boolean> {
    updating.value.push(key)
    try {
      await fn()
      await refresh?.()
      return true
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return false
      }
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== key)
    }
  }

  return { updating, run }
}

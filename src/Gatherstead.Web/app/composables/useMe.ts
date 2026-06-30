import { useRepositories } from '~/composables/useRepositories'
import type { MeSummary } from '~/repositories/types'

export function useMe() {
  const { me: repo } = useRepositories()
  const { data, pending, error, refresh } = useAsyncData<MeSummary>(
    'me',
    () => repo.getMe(),
  )
  return { me: computed(() => data.value ?? null), pending, error, refresh }
}

export function useMeActions(refresh?: () => Promise<void>) {
  const { me: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const saving = ref(false)

  async function updateDisplayName(displayName: string): Promise<boolean> {
    saving.value = true
    try {
      await repo.updateDisplayName(displayName)
      await refresh?.()
      toast.add({ title: t('account.saved'), color: 'success' })
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      saving.value = false
    }
  }

  return { saving, updateDisplayName }
}

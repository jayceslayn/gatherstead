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
  const { logout } = useAuth()
  const saving = ref(false)
  const deleting = ref(false)

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

  /**
   * Erases the account. Returns null on success (the federated logout then navigates away — the
   * landing page is the confirmation) or the localized error message so the caller can keep its
   * confirmation dialog open and show the reason inline (e.g. sole owner of a shared group).
   */
  async function deleteAccount(): Promise<string | null> {
    deleting.value = true
    try {
      await repo.deleteAccount()
      await logout()
      return null
    }
    catch (e) {
      deleting.value = false
      return translateError(e)
    }
  }

  return { saving, updateDisplayName, deleting, deleteAccount }
}

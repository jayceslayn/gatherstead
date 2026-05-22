import { useTenantStore } from '~/stores/tenant'
import { useRepositories } from '~/composables/useRepositories'

export function useTenantUserActions(refresh?: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()
  const updating = ref<string[]>([])

  async function setLinkedMember(userId: string, memberId: string | null) {
    updating.value.push(userId)
    try {
      await repo.setLinkedMember(tenantStore.currentTenantId!, userId, memberId)
      await refresh?.()
    }
    catch (e) {
      toast.add({ title: translateError(e as { code: string }), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== userId)
    }
  }

  return { updating, setLinkedMember }
}

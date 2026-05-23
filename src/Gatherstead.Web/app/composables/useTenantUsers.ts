import { useTenantStore } from '~/stores/tenant'
import { useRepositories } from '~/composables/useRepositories'
import type { TenantRole } from '~/repositories/types'

export function useTenantUserList() {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const { data, pending, error, refresh } = useAsyncData(
    () => `tenant-users-${tenantStore.currentTenantId}`,
    () => repo.listTenantUsers(tenantStore.currentTenantId!),
  )
  return { tenantUsers: computed(() => data.value ?? []), pending, error, refresh }
}

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
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== userId)
    }
  }

  async function updateRole(userId: string, role: TenantRole) {
    updating.value.push(userId)
    try {
      await repo.updateRole(tenantStore.currentTenantId!, userId, role)
      await refresh?.()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(k => k !== userId)
    }
  }

  return { updating, setLinkedMember, updateRole }
}

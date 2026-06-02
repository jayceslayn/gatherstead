import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import { useRepositories } from '~/composables/useRepositories'
import type { TenantRole, HouseholdRole, InvitationSummary } from '~/repositories/types'

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
  const memberStore = useCurrentMemberStore()
  const { tenantUsers: repo } = useRepositories()
  const toast = useToast()
  const { translateError } = useApiError()
  const config = useRuntimeConfig()
  const updating = ref<string[]>([])

  async function setLinkedMember(userId: string, memberId: string | null, householdId?: string) {
    updating.value.push(userId)
    try {
      await repo.setLinkedMember(tenantStore.currentTenantId!, userId, memberId)
      if (config.public.demoMode) {
        if (memberId && householdId) {
          memberStore.setLinkedMember(memberId, householdId)
        }
        else {
          memberStore.clear()
        }
      }
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

export function useInvitations() {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const { data, pending, error, refresh } = useAsyncData<InvitationSummary[]>(
    () => `invitations-${tenantStore.currentTenantId}`,
    () => repo.listInvitations(tenantStore.currentTenantId!),
    { watch: [() => tenantStore.currentTenantId] },
  )
  return { invitations: computed(() => data.value ?? []), pending, error, refresh }
}

export function useInvitationActions(refresh?: () => Promise<void>) {
  const tenantStore = useTenantStore()
  const { tenantUsers: repo } = useRepositories()
  const toast = useToast()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const saving = ref(false)

  async function invite(
    email: string,
    role: TenantRole,
    householdId: string | null = null,
    householdRole: HouseholdRole | null = null,
  ): Promise<boolean> {
    saving.value = true
    try {
      await repo.inviteUser(tenantStore.currentTenantId!, email, role, householdId, householdRole)
      await refresh?.()
      toast.add({ title: t('tenantUser.invite.sent'), color: 'success' })
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

  async function revoke(invitationId: string): Promise<void> {
    try {
      await repo.revokeInvitation(tenantStore.currentTenantId!, invitationId)
      await refresh?.()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
  }

  return { saving, invite, revoke }
}

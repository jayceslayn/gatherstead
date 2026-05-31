<script setup lang="ts">
import { useTenantUserList, useInvitations, useInvitationActions } from '~/composables/useTenantUsers'
import { useAllMembers } from '~/composables/useHouseholdMembers'
import type { TenantRole } from '~/repositories/types'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { tenantUsers, pending, refresh: refreshTenantUsers } = useTenantUserList()
const { memberMap } = useAllMembers()
const { invitations, refresh: refreshInvites } = useInvitations()
const { revoke } = useInvitationActions(refreshInvites)

// When an invite is sent to an existing user the API accepts it immediately and adds a
// TenantUser entry; refresh both lists so the new member appears without a page reload.
async function refreshAfterInvite() {
  await Promise.all([refreshInvites(), refreshTenantUsers()])
}

const showInvite = ref(false)
const pendingInvitations = computed(() => invitations.value.filter(i => i.status === 'Pending'))

const ROLE_ORDER: TenantRole[] = ['Owner', 'Manager', 'Coordinator', 'Member', 'Guest']

const search = ref('')
const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return tenantUsers.value
  return tenantUsers.value.filter(u => u.externalId.toLowerCase().includes(q))
})

const grouped = computed(() =>
  ROLE_ORDER
    .map(role => ({ role, users: filtered.value.filter(u => u.role === role) }))
    .filter(g => g.users.length > 0),
)
</script>

<template>
  <div>
    <GsBreadcrumb :items="[{ label: t('settings.title'), to: '/app/settings' }, { label: t('settings.users') }]" />
    <GsPageHeader :title="t('settings.users')">
      <GsRoleGate min-role="Manager">
        <UButton icon="i-heroicons-user-plus" size="sm" @click="showInvite = true">
          {{ t('tenantUser.invite.title') }}
        </UButton>
      </GsRoleGate>
    </GsPageHeader>

    <GsRoleGate min-role="Manager">
      <div v-if="pendingInvitations.length" class="mb-6">
        <p class="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
          {{ t('tenantUser.invite.pendingTitle') }}
        </p>
        <div class="flex flex-col gap-2">
          <UCard v-for="inv in pendingInvitations" :key="inv.id">
            <div class="flex items-center gap-3">
              <div class="min-w-0 flex-1">
                <p class="text-sm truncate">{{ inv.email }}</p>
                <p class="text-xs text-muted">{{ t(`tenantUser.roles.${inv.role}`) }}</p>
              </div>
              <UBadge color="warning" variant="subtle">{{ t('tenantUser.invite.statusPending') }}</UBadge>
              <UButton
                color="error"
                variant="ghost"
                size="xs"
                icon="i-heroicons-x-mark"
                :aria-label="t('tenantUser.invite.revoke')"
                @click="revoke(inv.id)"
              />
            </div>
          </UCard>
        </div>
      </div>
    </GsRoleGate>

    <div class="mb-4">
      <UInput
        v-model="search"
        :placeholder="t('common.search')"
        icon="i-heroicons-magnifying-glass"
        class="max-w-sm"
      />
    </div>

    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!tenantUsers.length"
      icon="i-heroicons-users"
      :title="t('tenantUser.title')"
      :description="t('tenantUser.noUsers')"
    />

    <GsEmptyState
      v-else-if="!filtered.length"
      icon="i-heroicons-magnifying-glass"
      :title="t('common.noResults')"
    />

    <div v-else class="flex flex-col gap-6">
      <div v-for="group in grouped" :key="group.role">
        <p class="text-xs font-semibold uppercase tracking-wider text-muted mb-2">
          {{ t(`tenantUser.roles.${group.role}`) }}
        </p>
        <div class="flex flex-col gap-2">
          <NuxtLink
            v-for="user in group.users"
            :key="user.userId"
            :to="`/app/settings/users/${user.userId}`"
          >
            <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer">
              <div class="flex items-center gap-3">
                <div class="min-w-0 flex-1">
                  <p class="font-mono text-sm truncate">{{ user.externalId }}</p>
                  <p class="text-xs text-muted">
                    {{ t('tenantUser.linkedMemberLabel') }}
                    {{ user.linkedMemberId ? (memberMap.get(user.linkedMemberId)?.name ?? user.linkedMemberId) : t('tenantUser.noLinkedMember') }}
                  </p>
                </div>
                <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
              </div>
            </UCard>
          </NuxtLink>
        </div>
      </div>
    </div>

    <GsInviteUserModal v-model:open="showInvite" :refresh="refreshAfterInvite" />
  </div>
</template>

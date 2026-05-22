<script setup lang="ts">
import { useTenantUserList } from '~/composables/useTenantUsers'
import { useAllMembers } from '~/composables/useHouseholdMembers'
import type { TenantRole } from '~/repositories/types'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { tenantUsers, pending } = useTenantUserList()
const { memberMap } = useAllMembers()

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
    <GsPageHeader :title="t('settings.users')" />

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
          <UCard v-for="user in group.users" :key="user.userId">
            <div class="flex items-center gap-3 flex-wrap">
              <div class="min-w-0 flex-1">
                <p class="font-mono text-sm truncate">{{ user.externalId }}</p>
                <p class="text-xs text-muted">
                  {{ t('tenantUser.linkedMemberLabel') }}
                  {{ user.linkedMemberId ? (memberMap.get(user.linkedMemberId)?.name ?? user.linkedMemberId) : t('tenantUser.noLinkedMember') }}
                </p>
              </div>
              <NuxtLink :to="`/app/settings/users/${user.userId}`">
                <UButton icon="i-heroicons-pencil-square" variant="ghost" size="sm" />
              </NuxtLink>
            </div>
          </UCard>
        </div>
      </div>
    </div>
  </div>
</template>

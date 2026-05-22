<script setup lang="ts">
import { useTenantUserList, useTenantUserActions } from '~/composables/useTenantUsers'
import type { TenantRole } from '~/repositories/types'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { tenantUsers, pending, refresh } = useTenantUserList()
const { updateRole, updating } = useTenantUserActions(refresh)

const search = ref('')
const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return tenantUsers.value
  return tenantUsers.value.filter(u => u.externalId.toLowerCase().includes(q))
})

const roleOptions: { label: string, value: TenantRole }[] = [
  { label: t('tenantUser.roles.Owner'), value: 'Owner' },
  { label: t('tenantUser.roles.Manager'), value: 'Manager' },
  { label: t('tenantUser.roles.Coordinator'), value: 'Coordinator' },
  { label: t('tenantUser.roles.Member'), value: 'Member' },
  { label: t('tenantUser.roles.Guest'), value: 'Guest' },
]
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

    <div v-else class="flex flex-col gap-2">
      <UCard v-for="user in filtered" :key="user.userId">
        <div class="flex items-center gap-3 flex-wrap">
          <div class="min-w-0 flex-1">
            <p class="font-mono text-sm truncate">{{ user.externalId }}</p>
            <p class="text-xs text-muted">{{ t('tenantUser.linkedMemberLabel') }} {{ user.linkedMemberId ?? t('tenantUser.noLinkedMember') }}</p>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <USelectMenu
              :model-value="user.role"
              :options="roleOptions"
              value-attribute="value"
              option-attribute="label"
              :disabled="updating.includes(user.userId)"
              class="w-36"
              @update:model-value="(v) => updateRole(user.userId, (v as { value: TenantRole }).value)"
            />
            <NuxtLink :to="`/app/settings/users/${user.userId}`">
              <UButton icon="i-heroicons-pencil-square" variant="ghost" size="sm" />
            </NuxtLink>
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>

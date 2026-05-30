<script setup lang="ts">
import type { HouseholdMember } from '~/repositories/types'
import { useTenantRole } from '~/composables/useTenantRole'
import { useHouseholds } from '~/composables/useHouseholds'
import { useAllMembers } from '~/composables/useHouseholdMembers'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { households, pending, refresh } = useHouseholds()
const { memberMap, pending: membersPending } = useAllMembers()

const search = ref('')
const showCreate = ref(false)

const membersByHousehold = computed(() => {
  const map = new Map<string, HouseholdMember[]>()
  for (const member of memberMap.value.values()) {
    const list = map.get(member.householdId)
    if (list) list.push(member)
    else map.set(member.householdId, [member])
  }
  return map
})

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return households.value
  return households.value.filter((h) => {
    if (h.name.toLowerCase().includes(q)) return true
    return (membersByHousehold.value.get(h.id) ?? []).some(m => m.name.toLowerCase().includes(q))
  })
})
</script>

<template>
  <div>
    <GsPageHeader :title="t('household.title')">
      <GsRoleGate min-role="Manager">
        <UButton icon="i-heroicons-plus" size="sm" @click="showCreate = true">
          {{ t('household.createTitle') }}
        </UButton>
      </GsRoleGate>
    </GsPageHeader>

    <div class="mb-4">
      <UInput
        v-model="search"
        :placeholder="t('household.searchPlaceholder')"
        icon="i-heroicons-magnifying-glass"
        class="max-w-sm"
      />
    </div>

    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!households.length"
      icon="i-heroicons-user-group"
      :title="t('household.noHouseholds')"
      :description="isManagerOrAbove ? t('household.noHouseholdsHintManager') : t('household.noHouseholdsHintMember')"
    >
      <UButton v-if="isManagerOrAbove" icon="i-heroicons-plus" @click="showCreate = true">
        {{ t('household.createTitle') }}
      </UButton>
    </GsEmptyState>

    <GsEmptyState
      v-else-if="!filtered.length"
      icon="i-heroicons-magnifying-glass"
      :title="t('common.noResults')"
    />

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <NuxtLink
        v-for="household in filtered"
        :key="household.id"
        :to="`/app/directory/${household.id}`"
      >
        <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer h-full">
          <div class="flex items-start gap-3">
            <GsMemberAvatar :name="household.name" size="md" class="shrink-0 mt-0.5" />
            <div class="min-w-0 flex-1">
              <p class="font-semibold truncate">{{ household.name }}</p>
              <p class="text-sm text-muted mt-0.5 line-clamp-2">
                {{ membersByHousehold.get(household.id)?.map(m => m.name).join(', ') || (!membersPending ? t('member.noMembers') : '') }}
              </p>
            </div>
            <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0 mt-0.5" />
          </div>
        </UCard>
      </NuxtLink>
    </div>

    <GsHouseholdModal v-model:open="showCreate" :refresh="refresh" />
  </div>
</template>

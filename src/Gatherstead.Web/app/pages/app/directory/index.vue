<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import { useHouseholds } from '~/composables/useHouseholds'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { households, pending } = useHouseholds()

const search = ref('')

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return households.value
  return households.value.filter(h => h.name.toLowerCase().includes(q))
})
</script>

<template>
  <div>
    <GsPageHeader :title="t('household.title')">
      <GsRoleGate min-role="Manager">
        <UButton to="/app/directory/create" icon="i-heroicons-plus" size="sm">
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
      <UButton v-if="isManagerOrAbove" to="/app/directory/create" icon="i-heroicons-plus">
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
          <div class="flex items-center gap-3">
            <GsMemberAvatar :name="household.name" size="md" />
            <div class="min-w-0 flex-1">
              <p class="font-semibold truncate">{{ household.name }}</p>
            </div>
            <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
          </div>
        </UCard>
      </NuxtLink>
    </div>
  </div>
</template>

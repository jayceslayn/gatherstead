<script setup lang="ts">
import { useProperties } from '~/composables/useProperties'
import { useTenantRole } from '~/composables/useTenantRole'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { properties, pending, refresh } = useProperties()

const showCreate = ref(false)

const search = ref('')
const filteredProperties = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return properties.value
  return properties.value.filter(p => p.name.toLowerCase().includes(q))
})
</script>

<template>
  <div>
    <GsPageHeader :title="t('property.title')">
      <GsRoleGate min-role="Manager">
        <UButton
          icon="i-heroicons-plus"
          size="sm"
          @click="() => { showCreate = true }"
        >
          {{ t('property.createTitle') }}
        </UButton>
      </GsRoleGate>
    </GsPageHeader>

    <div v-if="properties.length" class="mb-4">
      <GsSearchInput v-model="search" :placeholder="t('property.searchPlaceholder')" />
    </div>

    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!properties.length"
      icon="i-heroicons-building-office-2"
      :title="t('property.noProperties')"
      :description="isManagerOrAbove ? t('property.noPropertiesHintManager') : t('property.noPropertiesHintMember')"
    >
      <UButton v-if="isManagerOrAbove" icon="i-heroicons-plus" @click="() => { showCreate = true }">
        {{ t('property.createTitle') }}
      </UButton>
    </GsEmptyState>

    <GsEmptyState
      v-else-if="!filteredProperties.length"
      icon="i-heroicons-magnifying-glass"
      :title="t('common.noResults')"
    />

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <NuxtLink
        v-for="property in filteredProperties"
        :key="property.id"
        :to="`/app/properties/${property.id}`"
        class="block"
      >
        <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer h-full">
          <div class="flex items-center gap-3">
            <div class="rounded-lg bg-primary/10 p-2 shrink-0">
              <UIcon name="i-heroicons-building-office-2" class="size-5 text-primary" />
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-semibold truncate">{{ property.name }}</p>
            </div>
            <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
          </div>
        </UCard>
      </NuxtLink>
    </div>

    <GsPropertyModal v-model:open="showCreate" :refresh="refresh" />
  </div>
</template>

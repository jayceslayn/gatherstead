<script setup lang="ts">
import { useProperties } from '~/composables/useProperties'
import { useTenantRole } from '~/composables/useTenantRole'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { properties, pending } = useProperties()
</script>

<template>
  <div>
    <GsPageHeader :title="t('property.title')">
      <GsRoleGate min-role="Manager">
        <UButton
          to="/app/properties/create"
          icon="i-heroicons-plus"
          size="sm"
        >
          {{ t('property.addProperty') }}
        </UButton>
      </GsRoleGate>
    </GsPageHeader>

    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!properties.length"
      icon="i-heroicons-building-office-2"
      :title="t('property.noProperties')"
      :description="isManagerOrAbove ? t('property.noPropertiesHintManager') : t('property.noPropertiesHintMember')"
    >
      <UButton v-if="isManagerOrAbove" to="/app/properties/create" icon="i-heroicons-plus">
        {{ t('property.addProperty') }}
      </UButton>
    </GsEmptyState>

    <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <NuxtLink
        v-for="property in properties"
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
  </div>
</template>

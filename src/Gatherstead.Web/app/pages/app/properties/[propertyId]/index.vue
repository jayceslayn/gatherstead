<script setup lang="ts">
import { useProperty } from '~/composables/useProperties'
import { useAccommodations } from '~/composables/useAccommodations'
import { useTenantRole } from '~/composables/useTenantRole'
import type { AccommodationSummary, AccommodationType } from '~/repositories/types'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const route = useRoute()
const { isManagerOrAbove } = useTenantRole()

const propertyId = computed(() => route.params.propertyId as string)
const { property, pending: propertyPending } = useProperty(propertyId)
const { accommodations, pending: accommodationsPending } = useAccommodations(propertyId)

const TYPE_ORDER: AccommodationType[] = ['Bedroom', 'Bunk', 'RvPad', 'Tent', 'Offsite']

const grouped = computed(() => {
  const map = new Map<AccommodationType, AccommodationSummary[]>()
  for (const type of TYPE_ORDER) {
    const group = accommodations.value.filter(a => a.type === type)
    if (group.length) map.set(type, group)
  }
  return map
})

function typeLabel(type: AccommodationType): string {
  const key = type.charAt(0).toLowerCase() + type.slice(1)
  return t(`accommodation.types.${key}`)
}
</script>

<template>
  <div>
    <div v-if="propertyPending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="property">
      <GsBreadcrumb
        :items="[
          { label: t('property.title'), to: '/app/properties' },
          { label: property.name },
        ]"
      />

      <GsPageHeader :title="property.name">
        <GsRoleGate min-role="Manager">
          <UButton
            :to="`/app/properties/${property.id}/edit`"
            variant="outline"
            size="sm"
            icon="i-heroicons-pencil"
          >
            {{ t('common.edit') }}
          </UButton>
        </GsRoleGate>
      </GsPageHeader>

      <div v-if="accommodationsPending" class="py-8 text-center">
        <p class="text-muted">{{ t('common.loading') }}</p>
      </div>

      <GsEmptyState
        v-else-if="!accommodations.length"
        icon="i-heroicons-home"
        :title="t('property.noAccommodations')"
        :description="isManagerOrAbove ? t('property.noAccommodationsHintManager') : undefined"
      >
        <UButton
          v-if="isManagerOrAbove"
          :to="`/app/properties/${property.id}/accommodations/create`"
          icon="i-heroicons-plus"
        >
          {{ t('property.addAccommodation') }}
        </UButton>
      </GsEmptyState>

      <div v-else class="space-y-6">
        <div
          v-for="[type, group] in grouped"
          :key="type"
        >
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-sm font-semibold text-muted uppercase tracking-wide">
              {{ typeLabel(type) }}
            </h2>
            <GsRoleGate min-role="Manager">
              <UButton
                :to="`/app/properties/${property.id}/accommodations/create?type=${type}`"
                variant="ghost"
                size="xs"
                icon="i-heroicons-plus"
              >
                {{ t('property.addAccommodation') }}
              </UButton>
            </GsRoleGate>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            <GsAccommodationCard
              v-for="accommodation in group"
              :key="accommodation.id"
              :accommodation="accommodation"
              :link-to="`/app/properties/${property.id}/accommodations/${accommodation.id}/intents`"
            />
          </div>
        </div>
      </div>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>

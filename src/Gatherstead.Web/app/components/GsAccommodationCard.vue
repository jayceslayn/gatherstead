<script setup lang="ts">
import type { AccommodationSummary, AccommodationIntentStatus } from '~/repositories/types'

const props = defineProps<{
  accommodation: AccommodationSummary
  intentCount?: number
  topStatus?: AccommodationIntentStatus | null
  linkTo?: string
}>()

const { t } = useI18n()

const typeIcon: Record<string, string> = {
  Bedroom: 'i-heroicons-home',
  Bunk: 'i-heroicons-rectangle-stack',
  RvPad: 'i-heroicons-truck',
  Tent: 'i-heroicons-map',
  Offsite: 'i-heroicons-arrow-top-right-on-square',
}

const icon = computed(() => typeIcon[props.accommodation.type] ?? 'i-heroicons-home')

const capacityLabel = computed(() => {
  const { capacityAdults, capacityChildren } = props.accommodation
  const parts: string[] = []
  if (capacityAdults) parts.push(t('accommodation.adults', { n: capacityAdults }, capacityAdults))
  if (capacityChildren) parts.push(t('accommodation.children', { n: capacityChildren }, capacityChildren))
  return parts.join(', ')
})
</script>

<template>
  <component
    :is="linkTo ? 'NuxtLink' : 'div'"
    :to="linkTo"
    class="block"
  >
    <UCard
      :class="linkTo ? 'hover:ring-1 hover:ring-primary transition-all cursor-pointer h-full' : 'h-full'"
    >
      <div class="flex items-start gap-3">
        <div class="rounded-lg bg-primary/10 p-2 shrink-0">
          <UIcon :name="icon" class="size-5 text-primary" />
        </div>
        <div class="min-w-0 flex-1">
          <p class="font-semibold truncate">{{ accommodation.name }}</p>
          <p class="text-sm text-muted">{{ t(`accommodation.types.${accommodation.type.charAt(0).toLowerCase() + accommodation.type.slice(1)}`) }}</p>
          <p v-if="capacityLabel" class="text-xs text-muted mt-0.5">{{ capacityLabel }}</p>
        </div>
        <div class="flex flex-col items-end gap-1 shrink-0">
          <GsStatusBadge
            v-if="topStatus"
            :status="topStatus"
            size="xs"
          />
          <span v-if="intentCount !== undefined && intentCount > 0" class="text-xs text-muted">
            {{ t('accommodation.intentCount', { n: intentCount }, intentCount) }}
          </span>
          <UIcon v-if="linkTo" name="i-heroicons-chevron-right" class="size-4 text-muted mt-auto" />
        </div>
      </div>
      <p v-if="accommodation.notes" class="text-xs text-muted mt-2 line-clamp-2">
        {{ accommodation.notes }}
      </p>
    </UCard>
  </component>
</template>

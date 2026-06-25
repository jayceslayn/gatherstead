<script setup lang="ts">
import type { AccommodationAvailability } from '~/repositories/types'

const props = defineProps<{
  availability: AccommodationAvailability
  requesting?: boolean
}>()

const emit = defineEmits<{ request: [availability: AccommodationAvailability] }>()

const { t } = useI18n()

const typeIcon: Record<string, string> = {
  Bedroom: 'i-heroicons-home',
  Bunk: 'i-heroicons-rectangle-stack',
  RvPad: 'i-heroicons-truck',
  Tent: 'i-heroicons-map',
  Offsite: 'i-heroicons-arrow-top-right-on-square',
}

const icon = computed(() => typeIcon[props.availability.type] ?? 'i-heroicons-home')
const typeLabel = computed(() => {
  const ty = props.availability.type
  return t(`accommodation.types.${ty.charAt(0).toLowerCase() + ty.slice(1)}`)
})

// Per-dimension remaining label: a null capacity is unconstrained.
function remainingLabel(remaining: number | null, capacity: number | null): string {
  if (capacity == null) return t('accommodations.unlimited')
  return t('accommodations.remainingOf', { remaining: Math.max(remaining ?? 0, 0), capacity })
}
</script>

<template>
  <UCard :class="availability.hasSufficientCapacity ? 'h-full' : 'h-full opacity-60'">
    <div class="flex items-start gap-3">
      <div class="rounded-lg bg-primary/10 p-2 shrink-0">
        <UIcon :name="icon" class="size-5 text-primary" />
      </div>
      <div class="min-w-0 flex-1">
        <p class="font-semibold truncate">{{ availability.name }}</p>
        <p class="text-sm text-muted truncate">{{ `${availability.propertyName} · ${typeLabel}` }}</p>
      </div>
      <UBadge
        :color="availability.hasSufficientCapacity ? 'success' : 'neutral'"
        variant="subtle"
        size="xs"
      >
        {{ availability.hasSufficientCapacity ? t('accommodations.available') : t('accommodations.full') }}
      </UBadge>
    </div>

    <dl class="grid grid-cols-2 gap-2 mt-3 text-xs">
      <div>
        <dt class="text-muted">{{ t('accommodation.partyAdults') }}</dt>
        <dd>{{ remainingLabel(availability.remainingAdults, availability.capacityAdults) }}</dd>
      </div>
      <div>
        <dt class="text-muted">{{ t('accommodation.partyChildren') }}</dt>
        <dd>{{ remainingLabel(availability.remainingChildren, availability.capacityChildren) }}</dd>
      </div>
    </dl>

    <p v-if="availability.notes" class="text-xs text-muted mt-2 line-clamp-2">
      {{ availability.notes }}
    </p>

    <template #footer>
      <UButton
        block
        size="sm"
        icon="i-heroicons-plus"
        :loading="requesting"
        @click="emit('request', availability)"
      >
        {{ t('accommodation.requestStay') }}
      </UButton>
    </template>
  </UCard>
</template>

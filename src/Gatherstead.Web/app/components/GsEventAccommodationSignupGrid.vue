<script setup lang="ts">
import type { AccommodationSummary, AccommodationIntent, HouseholdMember } from '~/repositories/types'
import { OCCUPANCY_COLOR, occupancyState } from '~/composables/useReportView'

const props = defineProps<{
  days: string[]
  accommodations: AccommodationSummary[]
  members: HouseholdMember[]
  memberIntents: (accommodationId: string, night: string) => AccommodationIntent[]
  occupiedCount: (accommodationId: string, night: string) => number
  isUpdating: (key: string) => boolean
  /** Per-day going/maybe counts shown in the sticky header. */
  totalsByDay?: Record<string, { going: number, maybe: number }>
}>()

const emit = defineEmits<{
  edit: [intent: AccommodationIntent]
  cancel: [accommodationId: string, intentId: string]
}>()

// Shared with sibling signup grids so switching tabs preserves the mobile day pager.
const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()

const typeIcon: Record<string, string> = {
  Bedroom: 'i-heroicons-home',
  Bunk: 'i-heroicons-rectangle-stack',
  RvPad: 'i-heroicons-truck',
  Tent: 'i-heroicons-map',
  Offsite: 'i-heroicons-arrow-top-right-on-square',
}

const memberName = computed(() => {
  const map = new Map(props.members.map(m => [m.id, m.name]))
  return (id: string) => map.get(id) ?? id.slice(-8)
})

function capacity(acc: AccommodationSummary): number | null {
  return acc.capacityAdults != null || acc.capacityChildren != null
    ? (acc.capacityAdults ?? 0) + (acc.capacityChildren ?? 0)
    : null
}

function occupancyColor(acc: AccommodationSummary, day: string): 'neutral' | 'success' | 'warning' | 'error' {
  return OCCUPANCY_COLOR[occupancyState(props.occupiedCount(acc.id, day), capacity(acc))]
}

function partyLabel(intent: AccommodationIntent): string {
  const parts: string[] = []
  if (intent.partyAdults) parts.push(t('accommodation.adults', { n: intent.partyAdults }, intent.partyAdults))
  if (intent.partyChildren) parts.push(t('accommodation.children', { n: intent.partyChildren }, intent.partyChildren))
  return parts.join(' · ')
}
</script>

<template>
  <div v-if="!accommodations.length" class="py-6 text-center text-sm text-muted">
    {{ t('property.noAccommodations') }}
  </div>

  <GsSwimlaneGroup
    v-else
    v-model:selected-day-index="selectedDayIndex"
    :days="days"
  >
    <template #day-total="{ day }">
      <template v-if="totalsByDay?.[day]">
        <span class="inline-flex items-center gap-0.5">
          <UIcon name="i-heroicons-user-group" class="size-3 shrink-0" />
          {{ t('report.event.attendingCount', { n: totalsByDay[day]?.going ?? 0 }) }}
        </span>
        <span v-if="totalsByDay[day]?.maybe">{{ t('report.event.maybeCount', { n: totalsByDay[day]!.maybe }) }}</span>
      </template>
    </template>

    <GsSwimlane
      v-for="acc in accommodations"
      :key="acc.id"
      :title="acc.name"
    >
      <template #rule-leading>
        <UIcon :name="typeIcon[acc.type] ?? 'i-heroicons-home'" class="size-5 text-primary" />
      </template>

      <template #day="{ day }">
        <div class="space-y-2">
          <UBadge
            :color="occupancyColor(acc, day)"
            variant="subtle"
            icon="i-heroicons-user-group"
          >
            {{ capacity(acc) != null
              ? t('report.event.occupancy', { n: occupiedCount(acc.id, day), m: capacity(acc) })
              : t('report.event.occupantCount', { n: occupiedCount(acc.id, day) }) }}
          </UBadge>

          <ul v-if="memberIntents(acc.id, day).length" class="space-y-1">
            <li
              v-for="intent in memberIntents(acc.id, day)"
              :key="intent.id"
              class="flex items-center justify-between gap-2"
            >
              <button
                type="button"
                class="min-w-0 flex-1 text-left text-sm truncate hover:underline"
                @click="emit('edit', intent)"
              >
                {{ memberName(intent.householdMemberId) }}
              </button>
              <span class="flex items-center gap-1.5 shrink-0">
                <span v-if="partyLabel(intent)" class="text-xs text-muted">{{ partyLabel(intent) }}</span>
                <GsStatusBadge :status="intent.status" size="xs" />
                <UBadge
                  v-if="intent.decision !== 'Pending'"
                  :color="intent.decision === 'Approved' ? 'success' : 'error'"
                  variant="soft"
                  size="xs"
                >
                  {{ t(`accommodation.decision.${intent.decision.toLowerCase()}`) }}
                </UBadge>
                <UButton
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  square
                  icon="i-heroicons-pencil"
                  :aria-label="t('common.edit')"
                  @click="emit('edit', intent)"
                />
                <UButton
                  color="neutral"
                  variant="ghost"
                  size="xs"
                  square
                  icon="i-heroicons-x-mark"
                  :loading="isUpdating(intent.id)"
                  :aria-label="t('common.delete')"
                  @click="emit('cancel', acc.id, intent.id)"
                />
              </span>
            </li>
          </ul>
        </div>
      </template>
    </GsSwimlane>
  </GsSwimlaneGroup>
</template>

<script setup lang="ts">
import type { AccommodationSummary, AccommodationIntent, HouseholdMember } from '~/repositories/types'
import { OCCUPANCY_COLOR, occupancyState } from '~/composables/useReportView'

const props = defineProps<{
  day: string
  accommodations: AccommodationSummary[]
  members: HouseholdMember[]
  memberIntents: (accommodationId: string, night: string) => AccommodationIntent[]
  occupiedCount: (accommodationId: string, night: string) => number
  isUpdating: (key: string) => boolean
  attendance?: { going: number, maybe: number }
}>()

const emit = defineEmits<{
  request: [accommodationId: string, night: string]
  cancel: [accommodationId: string, intentId: string]
}>()

const { t } = useI18n()
const { formatDay } = useFormatDate()

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

function occupancyColor(acc: AccommodationSummary): 'neutral' | 'success' | 'warning' | 'error' {
  return OCCUPANCY_COLOR[occupancyState(props.occupiedCount(acc.id, props.day), capacity(acc))]
}
</script>

<template>
  <section class="flex flex-col">
    <header class="sticky top-0 z-10 bg-default border-b border-default pb-2 mb-3">
      <h3 class="font-semibold text-highlighted">{{ formatDay(day) }}</h3>
      <div v-if="attendance" class="flex items-center gap-3 text-sm text-muted mt-0.5">
        <span class="inline-flex items-center gap-1">
          <UIcon name="i-heroicons-user-group" class="size-4" />
          {{ t('report.event.attendingCount', { n: attendance.going }) }}
        </span>
        <span v-if="attendance.maybe">{{ t('report.event.maybeCount', { n: attendance.maybe }) }}</span>
      </div>
    </header>

    <p v-if="!accommodations.length" class="text-sm text-muted">{{ t('property.noAccommodations') }}</p>

    <div v-else class="space-y-2">
      <UCard
        v-for="acc in accommodations"
        :key="acc.id"
        :ui="{ body: 'p-3 sm:p-3' }"
      >
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0 flex items-start gap-2">
            <UIcon :name="typeIcon[acc.type] ?? 'i-heroicons-home'" class="size-5 shrink-0 mt-0.5 text-primary" />
            <div class="min-w-0">
              <p class="font-semibold truncate">{{ acc.name }}</p>
              <UBadge
                :color="occupancyColor(acc)"
                variant="subtle"
                icon="i-heroicons-user-group"
                class="mt-1"
              >
                {{ capacity(acc) != null
                  ? t('report.event.occupancy', { n: occupiedCount(acc.id, day), m: capacity(acc) })
                  : t('report.event.occupantCount', { n: occupiedCount(acc.id, day) }) }}
              </UBadge>
            </div>
          </div>
        </div>

        <!-- This household's requests for the night -->
        <ul v-if="memberIntents(acc.id, day).length" class="mt-3 space-y-1">
          <li
            v-for="intent in memberIntents(acc.id, day)"
            :key="intent.id"
            class="flex items-center justify-between gap-2"
          >
            <span class="text-sm truncate">{{ memberName(intent.householdMemberId) }}</span>
            <span class="flex items-center gap-1.5 shrink-0">
              <span v-if="intent.partySize" class="text-xs text-muted">{{ t('accommodation.partySizeValue', { n: intent.partySize }) }}</span>
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
                icon="i-heroicons-x-mark"
                :loading="isUpdating(intent.id)"
                :aria-label="t('common.delete')"
                @click="emit('cancel', acc.id, intent.id)"
              />
            </span>
          </li>
        </ul>

        <UButton
          variant="outline"
          size="xs"
          icon="i-heroicons-plus"
          class="mt-3"
          :loading="isUpdating(acc.id)"
          @click="emit('request', acc.id, day)"
        >
          {{ t('accommodation.requestStay') }}
        </UButton>
      </UCard>
    </div>
  </section>
</template>

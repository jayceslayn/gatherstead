<script setup lang="ts">
import type { EventReportAccommodation, EventReportOccupant } from '~/repositories/types'
import { accommodationOccupancy, OCCUPANCY_COLOR } from '~/composables/useReportView'

const props = defineProps<{
  acc: EventReportAccommodation
  expanded: Set<string>
}>()

const emit = defineEmits<{ toggle: [id: string] }>()

const { t } = useI18n()

// Expand state is keyed by accommodation only, so toggling any day's cell
// expands the whole lane — one click reveals a reservation's full span.
const key = computed(() => props.acc.accommodationId)
const occupancy = computed(() => accommodationOccupancy(props.acc))

function isExpanded(id: string) {
  return props.expanded.has(id)
}
// Detail stays in the DOM (hidden) so the print variant can reveal it without juggling state.
function detailClass(id: string, expandedClasses: string) {
  return isExpanded(id) ? expandedClasses : 'hidden print:block print:mt-3 print:space-y-2'
}

function partyLabel(occ: EventReportOccupant): string {
  const parts: string[] = []
  if (occ.partyAdults) parts.push(t('accommodation.adults', { n: occ.partyAdults }, occ.partyAdults))
  if (occ.partyChildren) parts.push(t('accommodation.children', { n: occ.partyChildren }, occ.partyChildren))
  return parts.join(' · ')
}
</script>

<template>
  <UCard :ui="{ body: 'p-3 sm:p-3' }" class="print:break-inside-avoid">
    <button
      type="button"
      class="w-full flex items-start justify-between gap-3 text-left"
      :aria-expanded="isExpanded(key)"
      :aria-label="isExpanded(key) ? t('report.event.hideDetails') : t('report.event.showDetails')"
      @click="emit('toggle', key)"
    >
      <div class="min-w-0">
        <UBadge
          :color="OCCUPANCY_COLOR[occupancy.state]"
          variant="subtle"
          icon="i-heroicons-user-group"
        >
          {{ occupancy.capacity != null
            ? t('report.event.occupancy', { n: acc.occupied, m: occupancy.capacity })
            : t('report.event.occupantCount', { n: acc.occupied }) }}
        </UBadge>
        <p v-if="acc.notes" class="text-xs text-muted mt-1.5 line-clamp-2 print:line-clamp-none">{{ acc.notes }}</p>
      </div>
      <UIcon
        v-if="acc.occupants.length"
        name="i-heroicons-chevron-down"
        class="size-5 shrink-0 mt-0.5 transition-transform print:hidden"
        :class="isExpanded(key) ? 'rotate-180' : ''"
      />
    </button>

    <div v-if="acc.occupants.length" :class="['text-sm', detailClass(key, 'mt-3')]">
      <ul class="space-y-0.5">
        <li v-for="occ in acc.occupants" :key="occ.memberId" class="flex items-center justify-between gap-2">
          <span>{{ occ.name }}</span>
          <span class="flex items-center gap-1.5">
            <span v-if="partyLabel(occ)" class="text-xs text-muted">{{ partyLabel(occ) }}</span>
            <GsStatusBadge :status="occ.status" icon-only />
          </span>
        </li>
      </ul>
    </div>
  </UCard>
</template>

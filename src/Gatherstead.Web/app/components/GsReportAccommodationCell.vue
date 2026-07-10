<script setup lang="ts">
import type { EventReportAccommodation, EventReportOccupant } from '~/repositories/types'
import { accommodationOccupancy, OCCUPANCY_COLOR } from '~/composables/useReportView'

// One accommodation on one night. Collapsed shows the headline occupancy badge;
// expanding the lane (or printing) reveals notes and the occupant list.
const props = defineProps<{
  acc: EventReportAccommodation
  expanded?: boolean
}>()

const { t } = useI18n()

const occupancy = computed(() => accommodationOccupancy(props.acc))

// Detail stays in the DOM (hidden) so the print variant can reveal it without juggling state.
const detailClass = computed(() =>
  props.expanded ? 'mt-3 space-y-2' : 'hidden print:block print:mt-3 print:space-y-2')

function partyLabel(occ: EventReportOccupant): string {
  const parts: string[] = []
  if (occ.partyAdults) parts.push(t('accommodation.adults', { n: occ.partyAdults }, occ.partyAdults))
  if (occ.partyChildren) parts.push(t('accommodation.children', { n: occ.partyChildren }, occ.partyChildren))
  return parts.join(' · ')
}
</script>

<template>
  <UCard :ui="{ body: 'p-3 sm:p-3' }" class="print:break-inside-avoid">
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
    </div>

    <div v-if="acc.notes || acc.occupants.length" :class="['text-sm', detailClass]">
      <p v-if="acc.notes" class="text-xs text-muted">{{ acc.notes }}</p>
      <ul v-if="acc.occupants.length" class="space-y-0.5">
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

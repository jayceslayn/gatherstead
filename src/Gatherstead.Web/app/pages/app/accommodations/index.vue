<script setup lang="ts">
import { useCurrentMemberStore } from '~/stores/member'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useAccommodationSearch, useAccommodationStayRequest } from '~/composables/useAccommodations'
import { useProperties } from '~/composables/useProperties'
import { useMyStays } from '~/composables/useMyUpcoming'
import type { AccommodationAvailability, AccommodationIntentStatus } from '~/repositories/types'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const route = useRoute()
const toast = useToast()
const memberStore = useCurrentMemberStore()

// ── Intake form (prefilled from query params when deep-linked from an event) ──
function todayStr(offsetDays = 0): string {
  const d = new Date()
  d.setDate(d.getDate() + offsetDays)
  return d.toISOString().substring(0, 10)
}

const startNight = ref<string>((route.query.startNight as string) || todayStr())
const endNight = ref<string>((route.query.endNight as string) || todayStr(1))
const partyAdults = ref<number | null>(route.query.adults ? Number(route.query.adults) : 1)
const partyChildren = ref<number | null>(route.query.children ? Number(route.query.children) : 0)
const requireCapacity = ref(true)

// Property filter — empty selection searches every property. Prefilled when deep-linked.
const { properties } = useProperties()
const propertyItems = computed(() => properties.value.map(p => ({ label: p.name, value: p.id })))
const selectedPropertyIds = ref<string[]>(
  route.query.propertyId ? [route.query.propertyId as string] : [],
)

const { results, hasSearched, params, pending, search, refresh: refreshSearch } = useAccommodationSearch()

const orderedNights = computed(() =>
  startNight.value <= endNight.value
    ? { start: startNight.value, end: endNight.value }
    : { start: endNight.value, end: startNight.value })

function runSearch() {
  const { start, end } = orderedNights.value
  search({
    startNight: start,
    endNight: end,
    partyAdults: partyAdults.value,
    partyChildren: partyChildren.value,
    requireCapacity: requireCapacity.value,
    propertyIds: selectedPropertyIds.value,
  })
}

// Re-run automatically when the capacity toggle flips after an initial search.
watch(requireCapacity, () => { if (hasSearched.value) runSearch() })

const resultSummary = computed(() => {
  const count = t('accommodations.resultCount', { n: results.value.length }, results.value.length)
  if (!params.value) return count
  const party = t('accommodations.forParty', {
    adults: params.value.partyAdults ?? 0,
    children: params.value.partyChildren ?? 0,
  })
  return `${count} · ${party}`
})

// Auto-search on first load when the form arrived prefilled from a deep link.
onMounted(() => {
  if (route.query.startNight || route.query.endNight || route.query.propertyId) runSearch()
})

// ── Stay request flow ────────────────────────────────────────────────────────
const linkedHouseholdId = computed(() => memberStore.linkedHouseholdId ?? '')
const { members } = useHouseholdMembers(linkedHouseholdId)
const { refresh: refreshStays } = useMyStays()
const { submitting, requestStay } = useAccommodationStayRequest()

const showRequestModal = ref(false)
const selected = ref<AccommodationAvailability | null>(null)

function openRequest(availability: AccommodationAvailability) {
  if (!memberStore.linkedMemberId) {
    toast.add({ title: t('accommodations.linkRequired'), color: 'warning' })
    return
  }
  selected.value = availability
  showRequestModal.value = true
}

const modalAccommodations = computed(() =>
  selected.value ? [{ ...selected.value, attributes: [] }] : [])

async function onSubmit(payload: {
  accommodationId: string
  memberId: string
  startNight: string
  endNight: string
  status: AccommodationIntentStatus
  partyAdults: number | null
  partyChildren: number | null
  notes: string | null
}) {
  const acc = selected.value
  if (!acc || !linkedHouseholdId.value) return
  const ok = await requestStay(
    acc.propertyId, payload.accommodationId, linkedHouseholdId.value, payload.memberId,
    payload.startNight, payload.endNight, payload.status, payload.partyAdults, payload.partyChildren, payload.notes,
  )
  if (ok) {
    showRequestModal.value = false
    toast.add({ title: t('accommodations.stayRequested'), color: 'success' })
    await Promise.all([refreshSearch(), refreshStays()])
  }
}
</script>

<template>
  <div>
    <GsPageHeader :title="t('accommodations.title')" :description="t('accommodations.subtitle')" />

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Search + results -->
      <div class="lg:col-span-2 space-y-6">
        <UCard>
          <UFormField :label="t('event.dateRangeLabel')">
            <GsDateRangePicker v-model:start-date="startNight" v-model:end-date="endNight" />
          </UFormField>
          <UFormField :label="t('accommodations.propertyFilter')" class="mt-4">
            <USelectMenu
              v-model="selectedPropertyIds"
              :items="propertyItems"
              value-key="value"
              :placeholder="t('accommodations.allProperties')"
              :content="{ side: 'bottom' }"
              multiple
              clear
              class="w-full"
            />
          </UFormField>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
            <UFormField :label="t('accommodation.partyAdults')">
              <UInput v-model.number="partyAdults" type="number" min="0" class="w-full" />
            </UFormField>
            <UFormField :label="t('accommodation.partyChildren')">
              <UInput v-model.number="partyChildren" type="number" min="0" class="w-full" />
            </UFormField>
          </div>
          <div class="flex items-center justify-between gap-3 mt-4 flex-wrap">
            <UCheckbox v-model="requireCapacity" :label="t('accommodations.onlyAvailable')" />
            <UButton icon="i-heroicons-magnifying-glass" :loading="pending" @click="runSearch">
              {{ t('common.search') }}
            </UButton>
          </div>
        </UCard>

        <div v-if="pending" class="py-12 text-center">
          <p class="text-muted">{{ t('common.loading') }}</p>
        </div>

        <GsEmptyState
          v-else-if="hasSearched && !results.length"
          icon="i-heroicons-home-modern"
          :title="t('accommodations.noResults')"
          :description="t('accommodations.noResultsHint')"
        />

        <div v-else-if="hasSearched" class="space-y-3">
          <p class="text-sm text-muted">{{ resultSummary }}</p>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <GsAccommodationAvailabilityCard
              v-for="item in results"
              :key="item.id"
              :availability="item"
              :requesting="submitting && selected?.id === item.id"
              @request="openRequest"
            />
          </div>
        </div>

        <GsEmptyState
          v-else
          icon="i-heroicons-magnifying-glass"
          :title="t('accommodations.startTitle')"
          :description="t('accommodations.startHint')"
        />
      </div>

      <!-- My upcoming stays -->
      <div class="lg:col-span-1">
        <GsMyUpcomingStays />
      </div>
    </div>

    <GsAccommodationRequestModal
      v-model:open="showRequestModal"
      :accommodations="modalAccommodations"
      :members="members"
      :event-days="[]"
      :default-member-id="memberStore.linkedMemberId"
      :default-start-night="orderedNights.start"
      :default-end-night="orderedNights.end"
      :default-party-adults="partyAdults"
      :default-party-children="partyChildren"
      :loading="submitting"
      @submit="onSubmit"
    />
  </div>
</template>

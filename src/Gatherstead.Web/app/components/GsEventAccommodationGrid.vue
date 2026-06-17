<script setup lang="ts">
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useAccommodations, useEventAccommodationSignup } from '~/composables/useAccommodations'
import { useCurrentMemberStore } from '~/stores/member'
import type { AccommodationIntent, AccommodationIntentStatus } from '~/repositories/types'

const props = defineProps<{
  propertyId: string
  days: string[]
  householdId: string
  /** Per-day going/maybe counts shown in the sticky header. */
  totalsByDay?: Record<string, { going: number, maybe: number }>
}>()

// Shared with sibling signup grids so switching tabs preserves the mobile day pager.
const selectedDayIndex = defineModel<number>('selectedDayIndex', { default: 0 })

const { t } = useI18n()
const memberStore = useCurrentMemberStore()

const propertyId = computed(() => props.propertyId)
const householdId = computed(() => props.householdId)

const { accommodations, pending: accommodationsPending } = useAccommodations(propertyId)

const { members: householdMembers } = useHouseholdMembers(householdId)
const memberIds = computed(() => householdMembers.value.map(m => m.id))

const {
  pending: accommodationSignupPending,
  memberIntents: accMemberIntents,
  occupiedCount: accOccupiedCount,
  requestStay,
  updateStay,
  cancelStay,
} = useEventAccommodationSignup(propertyId, accommodations, memberIds)

// Request-stay modal state. A single tab-level button creates a stay; clicking a stay edits it.
const requestModalOpen = ref(false)
const requestLoading = ref(false)
const deleteLoading = ref(false)
const editIntent = ref<AccommodationIntent | null>(null)

const defaultRequestMemberId = computed(() =>
  memberStore.linkedMemberId && memberIds.value.includes(memberStore.linkedMemberId)
    ? memberStore.linkedMemberId
    : null,
)

function openRequest() {
  editIntent.value = null
  requestModalOpen.value = true
}

function openEdit(intent: AccommodationIntent) {
  editIntent.value = intent
  requestModalOpen.value = true
}

async function submitRequest(payload: {
  id: string | null
  accommodationId: string
  memberId: string
  startNight: string
  endNight: string
  status: AccommodationIntentStatus
  partyAdults: number | null
  partyChildren: number | null
  notes: string | null
}) {
  if (!props.householdId) return
  requestLoading.value = true
  const ok = payload.id
    ? await updateStay(
        editIntent.value?.accommodationId ?? payload.accommodationId, // current location (path)
        payload.id,
        payload.memberId, // new member
        payload.accommodationId, // new (target) accommodation
        payload.startNight,
        payload.endNight,
        payload.status,
        editIntent.value?.decision ?? 'Pending',
        payload.notes,
        payload.partyAdults,
        payload.partyChildren,
      )
    : await requestStay(
        payload.accommodationId,
        props.householdId,
        payload.memberId,
        payload.startNight,
        payload.endNight,
        payload.status,
        payload.notes,
        payload.partyAdults,
        payload.partyChildren,
      )
  requestLoading.value = false
  if (ok) requestModalOpen.value = false
}

async function deleteIntent(intent: AccommodationIntent) {
  deleteLoading.value = true
  const ok = await cancelStay(intent.accommodationId, intent.id)
  deleteLoading.value = false
  if (ok) requestModalOpen.value = false
}
</script>

<template>
  <div v-if="accommodationsPending || accommodationSignupPending" class="py-8 text-center text-sm text-muted">
    {{ t('common.loading') }}
  </div>
  <GsEmptyState
    v-else-if="!accommodations.length"
    icon="i-heroicons-home"
    :title="t('property.noAccommodations')"
  />
  <template v-else>
    <div class="flex justify-end mb-3">
      <UButton
        size="sm"
        icon="i-heroicons-plus"
        @click="openRequest"
      >
        {{ t('accommodation.requestStay') }}
      </UButton>
    </div>

    <GsEventAccommodationSignupGrid
      v-model:selected-day-index="selectedDayIndex"
      :days="days"
      :accommodations="accommodations"
      :members="householdMembers"
      :member-intents="accMemberIntents"
      :occupied-count="accOccupiedCount"
      :totals-by-day="totalsByDay"
      @edit="openEdit"
    />
  </template>

  <GsAccommodationRequestModal
    v-model:open="requestModalOpen"
    :accommodations="accommodations"
    :members="householdMembers"
    :event-days="days"
    :default-member-id="defaultRequestMemberId"
    :edit-intent="editIntent"
    :loading="requestLoading"
    :delete-loading="deleteLoading"
    @submit="submitRequest"
    @delete="deleteIntent"
  />
</template>

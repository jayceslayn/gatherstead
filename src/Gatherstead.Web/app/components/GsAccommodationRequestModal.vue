<script setup lang="ts">
import type { AccommodationIntent, HouseholdMember, AccommodationIntentStatus } from '~/repositories/types'

// The picker only needs id + name, so this accepts both AccommodationSummary and availability results.
interface AccommodationOption { id: string, name: string }

const props = defineProps<{
  open: boolean
  accommodations: AccommodationOption[]
  members: HouseholdMember[]
  eventDays: string[]
  defaultMemberId: string | null
  /** Prefills the free-date night inputs for a new stay (e.g. from the availability search). */
  defaultStartNight?: string | null
  defaultEndNight?: string | null
  /** Prefills the guest counts for a new stay (e.g. from the availability search). */
  defaultPartyAdults?: number | null
  defaultPartyChildren?: number | null
  /** When set, the modal edits this stay as a unit instead of creating a new one. */
  editIntent?: AccommodationIntent | null
  loading?: boolean
  deleteLoading?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'delete': [intent: AccommodationIntent]
  'submit': [payload: {
    id: string | null
    accommodationId: string
    memberId: string
    startNight: string
    endNight: string
    status: AccommodationIntentStatus
    partyAdults: number | null
    partyChildren: number | null
    notes: string | null
  }]
}>()

const { t } = useI18n()

const accommodationId = ref('')
const memberId = ref('')
const status = ref<AccommodationIntentStatus>('Requested')
const partyAdults = ref<number | null>(null)
const partyChildren = ref<number | null>(null)
const notes = ref('')
const startNight = ref('')
const endNight = ref('')

const isEditing = computed(() => !!props.editIntent)

// No event context (e.g. the property accommodation page) → pick nights with free date inputs
// instead of selecting from a fixed event-day list.
const isFreeDates = computed(() => props.eventDays.length === 0)

// Second-click confirmation for the destructive delete (no entity removed on a single click).
const confirmDeleteOpen = ref(false)

// Reset / hydrate the form whenever the modal opens with fresh context.
watch(() => props.open, (isOpen) => {
  confirmDeleteOpen.value = false
  if (!isOpen) return
  const edit = props.editIntent
  if (edit) {
    accommodationId.value = edit.accommodationId
    memberId.value = edit.householdMemberId
    status.value = edit.status ?? 'Requested'
    partyAdults.value = edit.partyAdults ?? null
    partyChildren.value = edit.partyChildren ?? null
    notes.value = edit.notes ?? ''
    startNight.value = edit.startNight
    endNight.value = edit.endNight
    return
  }
  accommodationId.value = props.accommodations[0]?.id ?? ''
  memberId.value = props.defaultMemberId ?? props.members[0]?.id ?? ''
  status.value = 'Requested'
  partyAdults.value = props.defaultPartyAdults ?? null
  partyChildren.value = props.defaultPartyChildren ?? null
  notes.value = ''
  startNight.value = props.defaultStartNight ?? props.eventDays[0] ?? ''
  endNight.value = props.defaultEndNight ?? props.defaultStartNight ?? props.eventDays[0] ?? ''
})

const accommodationItems = computed(() => props.accommodations.map(a => ({ label: a.name, value: a.id })))
const memberItems = computed(() => props.members.map(m => ({ label: m.name, value: m.id })))

// In event mode, clamp the picker to the event's day span; empty strings (free mode) = no bound.
const dateMin = computed(() => props.eventDays[0] ?? '')
const dateMax = computed(() => props.eventDays.at(-1) ?? '')

const statusItems = computed(() => [
  { label: t('status.requested'), value: 'Requested' as AccommodationIntentStatus },
  { label: t('status.hold'), value: 'Hold' as AccommodationIntentStatus },
])

// Keep the span non-inverted: clamp the end night up to the start when the user picks an earlier one.
const orderedNights = computed(() => {
  if (!startNight.value || !endNight.value) return { start: startNight.value, end: endNight.value }
  return startNight.value <= endNight.value
    ? { start: startNight.value, end: endNight.value }
    : { start: endNight.value, end: startNight.value }
})

const nightCount = computed(() => {
  const { start, end } = orderedNights.value
  if (!start || !end) return 0
  if (isFreeDates.value) {
    const ms = Date.parse(end) - Date.parse(start)
    if (Number.isNaN(ms) || ms < 0) return 0
    return Math.floor(ms / 86_400_000) + 1
  }
  return props.eventDays.filter(d => d >= start && d <= end).length
})

const canSubmit = computed(() => !!accommodationId.value && !!memberId.value && nightCount.value > 0)

function submit() {
  if (!canSubmit.value) return
  const { start, end } = orderedNights.value
  emit('submit', {
    id: props.editIntent?.id ?? null,
    accommodationId: accommodationId.value,
    memberId: memberId.value,
    startNight: start,
    endNight: end,
    status: status.value,
    partyAdults: partyAdults.value,
    partyChildren: partyChildren.value,
    notes: notes.value.trim() || null,
  })
}

function confirmDelete() {
  if (props.editIntent) emit('delete', props.editIntent)
}
</script>

<template>
  <UModal
    :open="open"
    :title="isEditing ? t('accommodation.editStay') : t('accommodation.requestStay')"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <div class="space-y-4">
        <UFormField :label="t('accommodation.accommodation')">
          <USelect v-model="accommodationId" :items="accommodationItems" class="w-full" />
        </UFormField>

        <UFormField :label="t('event.signup.member')">
          <USelect v-model="memberId" :items="memberItems" class="w-full" />
        </UFormField>

        <UFormField :label="t('event.dateRangeLabel')">
          <GsDateRangePicker
            v-model:start-date="startNight"
            v-model:end-date="endNight"
            :min="dateMin"
            :max="dateMax"
          />
        </UFormField>
        <p class="text-xs text-muted">
          {{ t('event.signup.nightsSelected', { n: nightCount }) }}
        </p>

        <UFormField :label="t('accommodation.status')">
          <USelect v-model="status" :items="statusItems" class="w-full" />
        </UFormField>

        <div class="grid grid-cols-2 gap-3">
          <UFormField :label="t('accommodation.partyAdults')">
            <UInput v-model.number="partyAdults" type="number" min="0" class="w-full" />
          </UFormField>
          <UFormField :label="t('accommodation.partyChildren')">
            <UInput v-model.number="partyChildren" type="number" min="0" class="w-full" />
          </UFormField>
        </div>

        <UFormField :label="t('common.notes')">
          <UTextarea v-model="notes" :rows="2" class="w-full" />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <GsFormFooter
        :submit-label="isEditing ? t('common.save') : t('accommodation.requestStay')"
        :loading="loading"
        :disabled="!canSubmit"
        @submit="submit"
        @cancel="emit('update:open', false)"
      >
        <template v-if="isEditing" #delete>
          <UButton
            color="error"
            variant="ghost"
            icon="i-heroicons-trash"
            :loading="deleteLoading"
            @click="confirmDeleteOpen = true"
          >
            {{ t('accommodation.deleteStay') }}
          </UButton>
        </template>
      </GsFormFooter>
    </template>
  </UModal>

  <GsConfirmModal
    v-model:open="confirmDeleteOpen"
    :title="t('accommodation.deleteStay')"
    :description="t('accommodation.deleteStayConfirm')"
    :confirm-label="t('common.delete')"
    danger
    @confirm="confirmDelete"
  />
</template>

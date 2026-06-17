<script setup lang="ts">
import type { AccommodationSummary, AccommodationIntent, HouseholdMember, AccommodationIntentStatus } from '~/repositories/types'

const props = defineProps<{
  open: boolean
  accommodations: AccommodationSummary[]
  members: HouseholdMember[]
  eventDays: string[]
  defaultMemberId: string | null
  /** When set, the modal edits this stay as a unit instead of creating a new one. */
  editIntent?: AccommodationIntent | null
  loading?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
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
const { formatDate } = useFormatDate()

const accommodationId = ref('')
const memberId = ref('')
const status = ref<AccommodationIntentStatus>('Intent')
const partyAdults = ref<number | null>(null)
const partyChildren = ref<number | null>(null)
const notes = ref('')
const startNight = ref('')
const endNight = ref('')

const isEditing = computed(() => !!props.editIntent)

// Reset / hydrate the form whenever the modal opens with fresh context.
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  const edit = props.editIntent
  if (edit) {
    accommodationId.value = edit.accommodationId
    memberId.value = edit.householdMemberId
    status.value = edit.status ?? 'Intent'
    partyAdults.value = edit.partyAdults ?? null
    partyChildren.value = edit.partyChildren ?? null
    notes.value = edit.notes ?? ''
    startNight.value = edit.startNight
    endNight.value = edit.endNight
    return
  }
  accommodationId.value = props.accommodations[0]?.id ?? ''
  memberId.value = props.defaultMemberId ?? props.members[0]?.id ?? ''
  status.value = 'Intent'
  partyAdults.value = null
  partyChildren.value = null
  notes.value = ''
  startNight.value = props.eventDays[0] ?? ''
  endNight.value = props.eventDays[0] ?? ''
})

const accommodationItems = computed(() => props.accommodations.map(a => ({ label: a.name, value: a.id })))
const memberItems = computed(() => props.members.map(m => ({ label: m.name, value: m.id })))
const nightItems = computed(() => props.eventDays.map(d => ({ label: formatDate(d), value: d })))

const statusItems = computed(() => [
  { label: t('status.intent'), value: 'Intent' as AccommodationIntentStatus },
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

        <div class="grid grid-cols-2 gap-3">
          <UFormField :label="t('event.signup.firstNight')">
            <USelect v-model="startNight" :items="nightItems" class="w-full" />
          </UFormField>
          <UFormField :label="t('event.signup.lastNight')">
            <USelect v-model="endNight" :items="nightItems" class="w-full" />
          </UFormField>
        </div>
        <p class="text-xs text-muted -mt-2">
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
      <div class="flex justify-end gap-2">
        <UButton variant="outline" @click="emit('update:open', false)">{{ t('common.cancel') }}</UButton>
        <UButton :loading="loading" :disabled="!canSubmit" @click="submit">
          {{ isEditing ? t('common.save') : t('accommodation.requestStay') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

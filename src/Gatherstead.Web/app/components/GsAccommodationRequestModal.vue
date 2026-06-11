<script setup lang="ts">
import type { HouseholdMember, AccommodationIntentStatus } from '~/repositories/types'

const props = defineProps<{
  open: boolean
  accommodationName: string
  members: HouseholdMember[]
  eventDays: string[]
  defaultNight: string
  defaultMemberId: string | null
  loading?: boolean
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'submit': [payload: {
    memberId: string
    nights: string[]
    status: AccommodationIntentStatus
    partySize: number | null
    notes: string | null
  }]
}>()

const { t } = useI18n()
const { formatDate } = useFormatDate()

const memberId = ref('')
const status = ref<AccommodationIntentStatus>('Intent')
const partySize = ref<number | null>(null)
const notes = ref('')
const startNight = ref('')
const endNight = ref('')

// Reset the form whenever the modal opens with fresh context.
watch(() => props.open, (isOpen) => {
  if (!isOpen) return
  memberId.value = props.defaultMemberId ?? props.members[0]?.id ?? ''
  status.value = 'Intent'
  partySize.value = null
  notes.value = ''
  startNight.value = props.defaultNight || props.eventDays[0] || ''
  endNight.value = props.defaultNight || props.eventDays[0] || ''
})

const memberItems = computed(() => props.members.map(m => ({ label: m.name, value: m.id })))
const nightItems = computed(() => props.eventDays.map(d => ({ label: formatDate(d), value: d })))

const statusItems = computed(() => [
  { label: t('status.intent'), value: 'Intent' as AccommodationIntentStatus },
  { label: t('status.hold'), value: 'Hold' as AccommodationIntentStatus },
])

// Inclusive range of event nights between start and end (ISO strings sort lexically).
const selectedNights = computed(() => {
  if (!startNight.value || !endNight.value) return []
  const lo = startNight.value <= endNight.value ? startNight.value : endNight.value
  const hi = startNight.value <= endNight.value ? endNight.value : startNight.value
  return props.eventDays.filter(d => d >= lo && d <= hi)
})

const canSubmit = computed(() => !!memberId.value && selectedNights.value.length > 0)

function submit() {
  if (!canSubmit.value) return
  emit('submit', {
    memberId: memberId.value,
    nights: selectedNights.value,
    status: status.value,
    partySize: partySize.value,
    notes: notes.value.trim() || null,
  })
}
</script>

<template>
  <UModal
    :open="open"
    :title="t('accommodation.requestStay')"
    :description="accommodationName"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <div class="space-y-4">
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
          {{ t('event.signup.nightsSelected', { n: selectedNights.length }) }}
        </p>

        <UFormField :label="t('accommodation.status')">
          <USelect v-model="status" :items="statusItems" class="w-full" />
        </UFormField>

        <UFormField :label="t('accommodation.partySize')">
          <UInput v-model.number="partySize" type="number" min="1" class="w-full" />
        </UFormField>

        <UFormField :label="t('common.notes')">
          <UTextarea v-model="notes" :rows="2" class="w-full" />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton variant="outline" @click="emit('update:open', false)">{{ t('common.cancel') }}</UButton>
        <UButton :loading="loading" :disabled="!canSubmit" @click="submit">{{ t('accommodation.requestStay') }}</UButton>
      </div>
    </template>
  </UModal>
</template>

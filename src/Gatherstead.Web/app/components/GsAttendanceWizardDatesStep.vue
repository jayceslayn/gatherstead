<script setup lang="ts">
import type { HouseholdMember, AttendanceStatus } from '~/repositories/types'
import type { MemberWizardState } from '~/components/GsAttendanceWizardModal.vue'

const props = defineProps<{
  days: string[]
  members: HouseholdMember[]
}>()

const memberStates = defineModel<MemberWizardState[]>({ required: true })

const { t } = useI18n()
const { formatDate } = useFormatDate()

const dayItems = computed(() => props.days.map(d => ({ label: formatDate(d), value: d })))

// Household-level defaults used to bulk-fill all members.
const householdDayStatus = ref<AttendanceStatus>('Going')
const householdArrival = ref(props.days[0] ?? '')
const householdDeparture = ref(props.days.at(-1) ?? '')

function applyHouseholdDefaults() {
  memberStates.value = memberStates.value.map(s => ({
    ...s,
    dayStatus: householdDayStatus.value,
    arrival: householdArrival.value,
    departure: householdDeparture.value,
  }))
}

function memberNights(state: MemberWizardState): number {
  const lo = state.arrival <= state.departure ? state.arrival : state.departure
  const hi = state.arrival <= state.departure ? state.departure : state.arrival
  return props.days.filter(d => d >= lo && d <= hi).length
}

function updateMember(memberId: string, patch: Partial<MemberWizardState>) {
  memberStates.value = memberStates.value.map(s =>
    s.memberId === memberId ? { ...s, ...patch } : s,
  )
}
</script>

<template>
  <div class="space-y-5">
    <!-- Household defaults row -->
    <div class="rounded-lg border border-default bg-elevated p-4 space-y-4">
      <p class="text-sm font-medium">{{ t('event.attendanceWizard.householdDefaults') }}</p>

      <div class="space-y-3">
        <div class="flex items-center justify-between gap-3">
          <span class="text-sm text-muted">{{ t('event.attendanceWizard.stayLevel') }}</span>
          <GsAttendanceToggle
            :model-value="householdDayStatus"
            size="sm"
            @update:model-value="householdDayStatus = $event"
          />
        </div>

        <div v-if="householdDayStatus !== 'NotGoing'" class="grid grid-cols-2 gap-3">
          <UFormField :label="t('event.attendanceWizard.arrival')">
            <USelect v-model="householdArrival" :items="dayItems" class="w-full" />
          </UFormField>
          <UFormField :label="t('event.attendanceWizard.departure')">
            <USelect v-model="householdDeparture" :items="dayItems" class="w-full" />
          </UFormField>
        </div>
      </div>

      <UButton size="sm" variant="outline" @click="applyHouseholdDefaults">
        {{ t('event.attendanceWizard.applyToAll') }}
      </UButton>
    </div>

    <!-- Per-member rows -->
    <div class="space-y-3">
      <div
        v-for="state in memberStates"
        :key="state.memberId"
        class="rounded-lg border border-default p-4 space-y-3"
        :class="state.dayStatus === 'NotGoing' ? 'opacity-60' : ''"
      >
        <div class="flex items-center justify-between gap-3">
          <span class="text-sm font-medium min-w-0 truncate">
            {{ members.find(m => m.id === state.memberId)?.name ?? state.memberId }}
          </span>
          <GsAttendanceToggle
            :model-value="state.dayStatus"
            size="xs"
            class="shrink-0"
            @update:model-value="updateMember(state.memberId, { dayStatus: $event })"
          />
        </div>

        <div v-if="state.dayStatus !== 'NotGoing'" class="grid grid-cols-2 gap-3">
          <UFormField :label="t('event.attendanceWizard.arrival')">
            <USelect
              :model-value="state.arrival"
              :items="dayItems"
              class="w-full"
              @update:model-value="updateMember(state.memberId, { arrival: $event })"
            />
          </UFormField>
          <UFormField :label="t('event.attendanceWizard.departure')">
            <USelect
              :model-value="state.departure"
              :items="dayItems"
              class="w-full"
              @update:model-value="updateMember(state.memberId, { departure: $event })"
            />
          </UFormField>
        </div>

        <p v-if="state.dayStatus !== 'NotGoing'" class="text-xs text-muted">
          {{ t('event.attendanceWizard.daysSelected', { n: memberNights(state) }) }}
        </p>
      </div>
    </div>

  </div>
</template>

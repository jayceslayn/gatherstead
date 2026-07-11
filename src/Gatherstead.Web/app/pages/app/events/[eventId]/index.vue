<script setup lang="ts">
import { useHouseholds } from '~/composables/useHouseholds'
import { useCurrentMemberStore } from '~/stores/member'
import { useTenantRole } from '~/composables/useTenantRole'
import type { TabsItem } from '@nuxt/ui'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const memberStore = useCurrentMemberStore()
const { isManagerOrAbove } = useTenantRole()
const { households } = useHouseholds()

const eventId = computed(() => route.params.eventId as string)

const { event, pending: eventPending } = useEvent(eventId)

// Per-day attendance totals (going/maybe across all households) — reuse the report
// aggregation so the sign-up day headers match the event report exactly.
const { report } = useEventReport(eventId)
const attendanceByDay = computed(() => {
  const map: Record<string, { going: number, maybe: number }> = {}
  for (const d of report.value?.days ?? []) {
    map[d.day] = { going: d.going, maybe: d.maybe }
  }
  return map
})

// Tab state — computed so labels re-translate on locale switch.
const tabs = computed<TabsItem[]>(() => [
  { label: t('event.attendance'), value: 'attendance', slot: 'attendance', icon: 'i-heroicons-user-group' },
  { label: t('event.tasks'), value: 'tasks', slot: 'tasks', icon: 'i-heroicons-clipboard-document-list' },
  { label: t('event.accommodations'), value: 'accommodations', slot: 'accommodations', icon: 'i-heroicons-home' },
])

const activeTab = ref<string | number>(tabs.value[0]?.value ?? 0)

watch(activeTab, (newVal) => {
  const tab = tabs.value.find(tb => tb.value === newVal)
  if (tab) {
    history.replaceState(null, '', `#${tab.value}`)
  }
})

// Household selection — shared across all tabs
const selectedHouseholdId = ref<string>(memberStore.linkedHouseholdId ?? '')

watchEffect(() => {
  const first = households.value[0]
  if (!selectedHouseholdId.value && first) {
    selectedHouseholdId.value = first.id
  }
})

const manageableHouseholds = computed(() => {
  if (isManagerOrAbove.value) return households.value
  if (memberStore.linkedHouseholdId) {
    return households.value.filter(h => h.id === memberStore.linkedHouseholdId)
  }
  return []
})

const householdSelectItems = computed(() =>
  manageableHouseholds.value.map(h => ({ label: h.name, value: h.id })),
)

// Signup day pager — shared across the Attendance, Tasks and Accommodations swimlane
// grids so switching tabs doesn't reset the mobile pager back to day one.
const signupDayIndex = ref(0)

const eventDays = computed(() => {
  if (!event.value) return []
  const days: string[] = []
  const current = new Date(event.value.startDate + 'T00:00:00')
  const last = new Date(event.value.endDate + 'T00:00:00')
  while (current <= last) {
    days.push(current.toISOString().substring(0, 10))
    current.setDate(current.getDate() + 1)
  }
  return days
})

const { formatDateRange } = useFormatDate()

onMounted(() => {
  if (tabs.value.some(tab => tab.value === route.hash.substring(1))) {
    activeTab.value = route.hash.substring(1)
  }
})
</script>

<template>
  <div>
    <div v-if="eventPending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="event">
      <GsPageHeader :title="event.name">
        <GsRoleGate min-role="Coordinator">
          <UButton
            :to="`/app/events/${event.id}/edit`"
            variant="outline"
            size="sm"
            icon="i-heroicons-pencil"
          >
            {{ t('common.edit') }}
          </UButton>
        </GsRoleGate>
      </GsPageHeader>

      <div class="flex items-center gap-2 text-sm text-muted mb-4 flex-wrap">
        <UIcon name="i-heroicons-calendar-days" class="size-4 shrink-0" />
        <span>{{ formatDateRange(event.startDate, event.endDate) }}</span>
        <GsRoleGate min-role="Member">
          <UButton
            :to="`/app/reports/events/${event.id}`"
            variant="link"
            size="xs"
            icon="i-heroicons-chart-bar"
            class="ml-2"
          >
            {{ t('report.event.viewReport') }}
          </UButton>
        </GsRoleGate>
      </div>

      <GsNotesSection :notes="event.notes" class="mb-6 max-w-lg" />

      <GsAttributeSection :attributes="event.attributes" class="mb-6 max-w-lg" />

      <GsEmptyState
        v-if="!manageableHouseholds.length"
        icon="i-heroicons-home"
        :title="t('event.noHousehold.title')"
        :description="t('event.noHousehold.body')"
      />

      <div v-if="manageableHouseholds.length > 1" class="flex items-center gap-3 mb-6">
        <UFormField :label="t('event.selectHousehold')">
          <USelect
            v-model="selectedHouseholdId"
            :items="householdSelectItems"
            class="min-w-48 max-w-xs"
          />
        </UFormField>
      </div>

      <!-- On phones each trigger stacks a small label beneath its icon, like the mobile
           nav bar — matching the event report's tab selector. -->
      <UTabs
        v-if="manageableHouseholds.length"
        v-model="activeTab"
        :items="tabs"
        :ui="{ trigger: 'max-sm:flex-col max-sm:gap-0.5', label: 'max-sm:text-xs' }"
      >
        <template #attendance>
          <div class="mt-4 space-y-4">
            <GsDismissibleHint
              storage-key="gs-hint-event-signup-attendance"
              :title="t('event.hint.attendance.title')"
              :description="t('event.hint.attendance.body')"
            />
            <GsEventAttendanceGrid
              v-model:selected-day-index="signupDayIndex"
              :event-id="eventId"
              :days="eventDays"
              :household-id="selectedHouseholdId"
            />
          </div>
        </template>

        <template #tasks>
          <div class="mt-4 space-y-4">
            <GsDismissibleHint
              storage-key="gs-hint-event-signup-tasks"
              icon="i-heroicons-hand-raised"
              :title="t('event.hint.tasks.title')"
              :description="t('event.hint.tasks.body')"
            />
            <GsEventTaskSignupGrid
              v-model:selected-day-index="signupDayIndex"
              :event-id="eventId"
              :days="eventDays"
              :household-id="selectedHouseholdId"
              :totals-by-day="attendanceByDay"
            />
          </div>
        </template>

        <template #accommodations>
          <div class="mt-4 space-y-4">
            <GsMyUpcomingStays
              :filter-start="event.startDate"
              :filter-end="event.endDate"
              :show-heading="false"
              :limit="0"
              hide-when-empty
            />
            <UCard>
              <div class="flex flex-col items-center text-center gap-3 py-6">
                <div class="rounded-lg bg-primary/10 p-3">
                  <UIcon name="i-heroicons-home-modern" class="size-7 text-primary" />
                </div>
                <div class="max-w-md">
                  <p class="font-semibold">{{ t('event.accommodationsCtaTitle') }}</p>
                  <p class="text-sm text-muted mt-1">{{ t('event.accommodationsCtaBody') }}</p>
                </div>
                <UButton
                  :to="`/app/accommodations?startNight=${event.startDate}&endNight=${event.endDate}`"
                  icon="i-heroicons-magnifying-glass"
                >
                  {{ t('event.accommodationsCtaButton') }}
                </UButton>
              </div>
            </UCard>
          </div>
        </template>

      </UTabs>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>

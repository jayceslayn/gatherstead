<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import { useCurrentMemberStore } from '~/stores/member'
import type { AttendanceStatus } from '~/composables/useEventAttendance'
import { useMealTemplates } from '~/composables/useMealPlans'
import { useChoreTemplates } from '~/composables/useChoreTemplates'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const { isManagerOrAbove } = useTenantRole()
const memberStore = useCurrentMemberStore()

const eventId = computed(() => route.params.eventId as string)

const { event, pending: eventPending } = useEvent(eventId)
const { attendance, upsert } = useEventAttendance(eventId)
const { templates: mealTemplates, pending: mealTemplatesPending } = useMealTemplates(eventId)
const { templates: choreTemplates, pending: choreTemplatesPending } = useChoreTemplates(eventId)

const tabs = computed(() => [
  { label: t('event.overview'), slot: 'overview' as const },
  { label: t('event.meals'), slot: 'meals' as const },
  { label: t('event.chores'), slot: 'chores' as const },
  { label: t('event.accommodations'), slot: 'accommodations' as const },
])

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

const myAttendanceByDay = computed<Record<string, AttendanceStatus>>(() => {
  if (!memberStore.linkedMemberId) return {}
  return Object.fromEntries(
    attendance.value
      .filter(a => a.householdMemberId === memberStore.linkedMemberId)
      .map(a => [a.day, a.status]),
  )
})

const calendarEvents = computed(() => {
  if (!event.value) return []
  const end = new Date(event.value.endDate + 'T00:00:00')
  end.setDate(end.getDate() + 1)
  return [{
    id: event.value.id,
    title: event.value.name,
    start: event.value.startDate,
    end: end.toISOString().split('T')[0],
  }]
})

const attendanceUpdating = ref<Record<string, boolean>>({})

async function toggleAttendance(day: string, status: AttendanceStatus) {
  if (!memberStore.linkedMemberId || !memberStore.linkedHouseholdId) return
  attendanceUpdating.value[day] = true
  try {
    await upsert(memberStore.linkedHouseholdId, memberStore.linkedMemberId, day, status)
  }
  finally {
    attendanceUpdating.value[day] = false
  }
}

function formatHeader(date: string) {
  return new Intl.DateTimeFormat(undefined, { month: 'long', day: 'numeric', year: 'numeric' }).format(
    new Date(date + 'T00:00:00'),
  )
}

function formatDayLabel(date: string) {
  return new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
    new Date(date + 'T00:00:00'),
  )
}
</script>

<template>
  <div>
    <div v-if="eventPending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="event">
      <GsPageHeader :title="event.name">
        <GsRoleGate min-role="Manager">
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

      <div class="flex items-center gap-2 text-sm text-muted mb-6">
        <UIcon name="i-heroicons-calendar-days" class="size-4 shrink-0" />
        <span>{{ formatHeader(event.startDate) }} – {{ formatHeader(event.endDate) }}</span>
      </div>

      <UTabs :items="tabs">
        <template #overview>
          <div class="mt-4 space-y-6">
            <GsCalendar
              :events="calendarEvents"
              initial-view="dayGridMonth"
              :initial-date="event.startDate"
            />

            <div v-if="memberStore.linkedMemberId">
              <h3 class="text-sm font-semibold mb-3">{{ t('event.myAttendance') }}</h3>
              <div class="divide-y divide-(--ui-border)">
                <div
                  v-for="day in eventDays"
                  :key="day"
                  class="flex items-center justify-between gap-4 py-2.5"
                >
                  <span class="text-sm">{{ formatDayLabel(day) }}</span>
                  <GsAttendanceToggle
                    :model-value="myAttendanceByDay[day] ?? null"
                    :loading="attendanceUpdating[day]"
                    @update:model-value="toggleAttendance(day, $event)"
                  />
                </div>
              </div>
            </div>
          </div>
        </template>

        <template #meals>
          <div class="mt-4">
            <div v-if="mealTemplatesPending" class="py-8 text-center text-sm text-muted">
              {{ t('common.loading') }}
            </div>
            <GsEmptyState
              v-else-if="!mealTemplates.length"
              icon="i-heroicons-cake"
              :title="t('event.meal.noTemplates')"
              class="mt-4"
            />
            <div v-else class="space-y-4">
              <GsMealTemplateSection
                v-for="template in mealTemplates"
                :key="template.id"
                :template="template"
                :event-id="eventId"
              />
            </div>
          </div>
        </template>

        <template #chores>
          <div class="mt-4">
            <div v-if="choreTemplatesPending" class="py-8 text-center text-sm text-muted">
              {{ t('common.loading') }}
            </div>
            <GsEmptyState
              v-else-if="!choreTemplates.length"
              icon="i-heroicons-clipboard-document-list"
              :title="t('event.chore.noTemplates')"
              class="mt-4"
            />
            <div v-else class="space-y-4">
              <GsChoreTemplateSection
                v-for="template in choreTemplates"
                :key="template.id"
                :template="template"
                :event-id="eventId"
              />
            </div>
          </div>
        </template>

        <template #accommodations>
          <GsEmptyState
            icon="i-heroicons-home"
            :title="t('event.accommodations')"
            :description="t('event.tabsComingSoon')"
            class="mt-4"
          />
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

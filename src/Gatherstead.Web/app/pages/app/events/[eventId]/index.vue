<script setup lang="ts">
import { useMealTemplates } from '~/composables/useMealPlans'
import { useChoreTemplates } from '~/composables/useChoreTemplates'
import { useHouseholds } from '~/composables/useHouseholds'
import { useCurrentMemberStore } from '~/stores/member'
import { useTenantRole } from '~/composables/useTenantRole'

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
const { templates: mealTemplates, pending: mealTemplatesPending } = useMealTemplates(eventId)
const { templates: choreTemplates, pending: choreTemplatesPending } = useChoreTemplates(eventId)

const tabs = computed(() => [
  { label: t('event.attendance'), slot: 'attendance' as const },
  { label: t('event.meals'), slot: 'meals' as const },
  { label: t('event.chores'), slot: 'chores' as const },
  { label: t('event.accommodations'), slot: 'accommodations' as const },
])

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

function formatHeader(date: string) {
  return new Intl.DateTimeFormat(undefined, { month: 'long', day: 'numeric', year: 'numeric' }).format(
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

      <div class="flex items-center gap-2 text-sm text-muted mb-4">
        <UIcon name="i-heroicons-calendar-days" class="size-4 shrink-0" />
        <span>{{ formatHeader(event.startDate) }} – {{ formatHeader(event.endDate) }}</span>
      </div>

      <div v-if="manageableHouseholds.length > 1" class="flex items-center gap-3 mb-6">
        <UFormField :label="t('event.selectHousehold')">
          <USelect
            v-model="selectedHouseholdId"
            :items="householdSelectItems"
            class="min-w-48 max-w-xs"
          />
        </UFormField>
      </div>

      <UTabs :items="tabs">
        <template #attendance>
          <div class="mt-4">
            <GsEventAttendanceGrid
              :event-id="eventId"
              :days="eventDays"
              :household-id="selectedHouseholdId"
            />
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

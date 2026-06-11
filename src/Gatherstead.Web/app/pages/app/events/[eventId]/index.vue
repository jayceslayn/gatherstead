<script setup lang="ts">
import { useTaskTemplates } from '~/composables/useTaskTemplates'
import { useHouseholds } from '~/composables/useHouseholds'
import { useAccommodations } from '~/composables/useAccommodations'
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
const { templates: taskTemplates, pending: taskTemplatesPending } = useTaskTemplates(eventId)

const eventPropertyId = computed(() => event.value?.propertyId ?? '')
const { accommodations, pending: accommodationsPending } = useAccommodations(eventPropertyId)

// Tab state — computed so labels re-translate on locale switch.
const tabs = computed<TabsItem[]>(() => [
  { label: t('event.attendance'), value: 'attendance', slot: 'attendance' },
  { label: t('event.tasks'), value: 'tasks', slot: 'tasks' },
  { label: t('event.accommodations'), value: 'accommodations', slot: 'accommodations' },
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

function formatHeader(date: string) {
  return new Intl.DateTimeFormat(undefined, { month: 'long', day: 'numeric', year: 'numeric' }).format(
    new Date(date + 'T00:00:00'),
  )
}

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
        <span>{{ t('event.dateRange', { start: formatHeader(event.startDate), end: formatHeader(event.endDate) }) }}</span>
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

      <div v-if="manageableHouseholds.length > 1" class="flex items-center gap-3 mb-6">
        <UFormField :label="t('event.selectHousehold')">
          <USelect
            v-model="selectedHouseholdId"
            :items="householdSelectItems"
            class="min-w-48 max-w-xs"
          />
        </UFormField>
      </div>

      <UTabs
        v-model="activeTab"
        :items="tabs"
      >
        <template #attendance>
          <div class="mt-4">
            <GsEventAttendanceGrid
              :event-id="eventId"
              :days="eventDays"
              :household-id="selectedHouseholdId"
            />
          </div>
        </template>

        <template #tasks>
          <div class="mt-4">
            <div v-if="taskTemplatesPending" class="py-8 text-center text-sm text-muted">
              {{ t('common.loading') }}
            </div>
            <GsEmptyState
              v-else-if="!taskTemplates.length"
              icon="i-heroicons-clipboard-document-list"
              :title="t('event.task.noTemplates')"
            />
            <div v-else class="space-y-4">
              <GsTaskTemplateSection
                v-for="template in taskTemplates"
                :key="template.id"
                :template="template"
                :event-id="eventId"
              />
            </div>
          </div>
        </template>

        <template #accommodations>
          <div class="mt-4">
            <div v-if="accommodationsPending" class="py-8 text-center text-sm text-muted">
              {{ t('common.loading') }}
            </div>
            <GsEmptyState
              v-else-if="!accommodations.length"
              icon="i-heroicons-home"
              :title="t('property.noAccommodations')"
            />
            <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              <GsAccommodationCard
                v-for="accommodation in accommodations"
                :key="accommodation.id"
                :accommodation="accommodation"
                :link-to="`/app/properties/${eventPropertyId}/accommodations/${accommodation.id}/intents`"
              />
            </div>
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

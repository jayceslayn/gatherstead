<script setup lang="ts">
import { useMealTemplates, useMealTemplateActions } from '~/composables/useMealPlans'
import { useTaskTemplates, useTaskTemplateActions } from '~/composables/useTaskTemplates'
import { useHouseholds } from '~/composables/useHouseholds'
import { useAccommodations } from '~/composables/useAccommodations'
import { useCurrentMemberStore } from '~/stores/member'
import { useTenantRole } from '~/composables/useTenantRole'
import { mealTypesFromFlags } from '~/repositories/types'
import type { MealTemplate, TaskTemplate } from '~/repositories/types'
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
const { templates: mealTemplates, pending: mealTemplatesPending, refresh: refreshMealTemplates } = useMealTemplates(eventId)
const { templates: taskTemplates, pending: taskTemplatesPending, refresh: refreshTaskTemplates } = useTaskTemplates(eventId)

const { updating: mealUpdating, deleteTemplate: deleteMealTemplate } = useMealTemplateActions(eventId, refreshMealTemplates)
const { updating: taskUpdating, deleteTemplate: deleteTaskTemplate } = useTaskTemplateActions(eventId, refreshTaskTemplates)

const eventPropertyId = computed(() => event.value?.propertyId ?? '')
const { accommodations, pending: accommodationsPending } = useAccommodations(eventPropertyId)

// Tab state — computed so labels re-translate on locale switch.
const tabs = computed<TabsItem[]>(() => [
  { label: t('event.attendance'), value: 'attendance', slot: 'attendance' },
  { label: t('event.meals'), value: 'meals', slot: 'meals' },
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

function formatRange(start: string | null, end: string | null): string | null {
  if (!start || !end) return null
  const fmt = (d: string) => new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(new Date(d + 'T00:00:00'))
  return start === end ? fmt(start) : t('event.meal.dateRange', { start: fmt(start), end: fmt(end) })
}

function mealTypeLabels(template: MealTemplate): string {
  return mealTypesFromFlags(template.mealTypes).map(mt => t(`event.meal.${mt.toLowerCase()}`)).join(', ')
}

// Meal template modal
const showMealModal = ref(false)
const editingMealTemplate = ref<MealTemplate | null>(null)
function openCreateMeal() {
  editingMealTemplate.value = null
  showMealModal.value = true
}
function openEditMeal(template: MealTemplate) {
  editingMealTemplate.value = template
  showMealModal.value = true
}

// Task template modal
const showTaskModal = ref(false)
const editingTaskTemplate = ref<TaskTemplate | null>(null)
function openCreateTask() {
  editingTaskTemplate.value = null
  showTaskModal.value = true
}
function openEditTask(template: TaskTemplate) {
  editingTaskTemplate.value = template
  showTaskModal.value = true
}

// Delete confirms
const mealToDelete = ref<MealTemplate | null>(null)
const taskToDelete = ref<TaskTemplate | null>(null)
async function confirmDeleteMeal() {
  const tpl = mealToDelete.value
  mealToDelete.value = null
  if (tpl) await deleteMealTemplate(tpl.id)
}
async function confirmDeleteTask() {
  const tpl = taskToDelete.value
  taskToDelete.value = null
  if (tpl) await deleteTaskTemplate(tpl.id)
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

        <template #meals>
          <div class="mt-4">
            <div class="flex justify-end mb-4">
              <GsRoleGate min-role="Coordinator">
                <UButton icon="i-heroicons-plus" size="sm" @click="openCreateMeal">
                  {{ t('event.meal.addTemplate') }}
                </UButton>
              </GsRoleGate>
            </div>

            <div v-if="mealTemplatesPending" class="py-8 text-center text-sm text-muted">
              {{ t('common.loading') }}
            </div>
            <GsEmptyState
              v-else-if="!mealTemplates.length"
              icon="i-heroicons-cake"
              :title="t('event.meal.noTemplates')"
            />
            <div v-else class="space-y-3">
              <UCard v-for="template in mealTemplates" :key="template.id">
                <div class="flex items-start justify-between gap-4">
                  <div class="min-w-0">
                    <div class="flex items-center gap-2 flex-wrap">
                      <p class="font-semibold">{{ template.name }}</p>
                      <span v-if="formatRange(template.startDate, template.endDate)" class="text-xs text-muted">
                        {{ formatRange(template.startDate, template.endDate) }}
                      </span>
                    </div>
                    <p class="text-sm text-muted mt-0.5">{{ mealTypeLabels(template) }}</p>
                    <p v-if="template.notes" class="text-sm text-muted mt-1">{{ template.notes }}</p>
                  </div>
                  <GsRoleGate min-role="Coordinator">
                    <div class="flex items-center gap-1 shrink-0">
                      <UButton
                        variant="ghost"
                        size="xs"
                        icon="i-heroicons-pencil"
                        :aria-label="t('common.edit')"
                        @click="openEditMeal(template)"
                      />
                      <UButton
                        color="error"
                        variant="ghost"
                        size="xs"
                        icon="i-heroicons-trash"
                        :aria-label="t('common.delete')"
                        :loading="mealUpdating.includes(template.id)"
                        @click="mealToDelete = template"
                      />
                    </div>
                  </GsRoleGate>
                </div>
              </UCard>
            </div>
          </div>
        </template>

        <template #tasks>
          <div class="mt-4">
            <div class="flex justify-end mb-4">
              <GsRoleGate min-role="Coordinator">
                <UButton icon="i-heroicons-plus" size="sm" @click="openCreateTask">
                  {{ t('event.task.addTemplate') }}
                </UButton>
              </GsRoleGate>
            </div>

            <div v-if="taskTemplatesPending" class="py-8 text-center text-sm text-muted">
              {{ t('common.loading') }}
            </div>
            <GsEmptyState
              v-else-if="!taskTemplates.length"
              icon="i-heroicons-clipboard-document-list"
              :title="t('event.task.noTemplates')"
            />
            <div v-else class="space-y-4">
              <div v-for="template in taskTemplates" :key="template.id">
                <GsRoleGate min-role="Coordinator">
                  <div class="flex items-center justify-end gap-1 mb-1">
                    <UButton
                      variant="ghost"
                      size="xs"
                      icon="i-heroicons-pencil"
                      :aria-label="t('common.edit')"
                      @click="openEditTask(template)"
                    >
                      {{ t('common.edit') }}
                    </UButton>
                    <UButton
                      color="error"
                      variant="ghost"
                      size="xs"
                      icon="i-heroicons-trash"
                      :aria-label="t('common.delete')"
                      :loading="taskUpdating.includes(template.id)"
                      @click="taskToDelete = template"
                    />
                  </div>
                </GsRoleGate>
                <GsTaskTemplateSection
                  :template="template"
                  :event-id="eventId"
                />
              </div>
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

      <GsMealTemplateModal
        v-model:open="showMealModal"
        :event-id="eventId"
        :template="editingMealTemplate"
        :refresh="refreshMealTemplates"
        :refresh-tasks="refreshTaskTemplates"
      />
      <GsTaskTemplateModal
        v-model:open="showTaskModal"
        :event-id="eventId"
        :template="editingTaskTemplate"
        :refresh="refreshTaskTemplates"
      />
      <GsConfirmModal
        :open="!!mealToDelete"
        :title="t('event.meal.deleteTemplate')"
        :description="t('event.meal.deleteConfirm')"
        :confirm-label="t('common.delete')"
        danger
        @update:open="val => { if (!val) mealToDelete = null }"
        @confirm="confirmDeleteMeal"
      />
      <GsConfirmModal
        :open="!!taskToDelete"
        :title="t('event.task.deleteTemplate')"
        :description="t('event.task.deleteConfirm')"
        :confirm-label="t('common.delete')"
        danger
        @update:open="val => { if (!val) taskToDelete = null }"
        @confirm="confirmDeleteTask"
      />
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>

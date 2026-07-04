<script setup lang="ts">
import type { EventSummary, MealTemplate, TaskTemplate, AttributeWriteEntry } from '~/repositories/types'
import { useProperties } from '~/composables/useProperties'
import { useSortedMealTemplates, useMealTemplateActions } from '~/composables/useMealPlans'
import { useSortedTaskTemplates, useTaskTemplateActions } from '~/composables/useTaskTemplates'
import { useTenantRole } from '~/composables/useTenantRole'
import { toAttributeWriteEntries, cleanAttributeWriteEntries, hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'
import type { TabsItem } from '@nuxt/ui'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const router = useRouter()

const eventId = computed(() => route.params.eventId as string)
const { event, pending, refresh } = useEvent(eventId)
const { properties } = useProperties()
const { refresh: refreshList } = useEvents()
const { updating, updateEvent, deleteEvent } = useEventActions(refreshList)
const { isManagerOrAbove } = useTenantRole()

const saving = computed(() => updating.value.includes(eventId.value))
const showDeleteConfirm = ref(false)

// === Details form ===
const form = reactive({
  propertyId: '',
  name: '',
  startDate: '',
  endDate: '',
  notes: '',
  attributes: [] as AttributeWriteEntry[],
})

const errors = reactive({ name: '', dates: '' })

const propertyItems = computed(() =>
  properties.value.map(p => ({ label: p.name, value: p.id })),
)

watch(event, (val: EventSummary | null) => {
  if (!val) return
  form.propertyId = val.propertyId
  form.name = val.name
  form.startDate = val.startDate
  form.endDate = val.endDate
  form.notes = val.notes ?? ''
  form.attributes = toAttributeWriteEntries(val.attributes)
}, { immediate: true })

function validate(): boolean {
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('event.name') })
  // The range picker guarantees start <= end, so only presence needs checking.
  errors.dates = (!form.startDate || !form.endDate)
    ? t('validation.required', { field: t('event.dateRangeLabel') })
    : ''
  return !errors.name && !errors.dates && !hasIncompleteAttributeRows(form.attributes)
}

async function onSubmit() {
  if (!validate()) return
  const ok = await updateEvent(
    eventId.value, form.name.trim(), form.startDate, form.endDate,
    form.notes.trim() || null, cleanAttributeWriteEntries(form.attributes),
  )
  if (ok) {
    await refresh()
    await navigateTo(`/app/events/${eventId.value}`)
  }
}

async function confirmDelete() {
  showDeleteConfirm.value = false
  await deleteEvent(eventId.value)
  await router.push('/app/events')
}

// === Sub-tabs — Details (Manager only) / Meals / Tasks ===
const tabs = computed<TabsItem[]>(() => [
  ...(isManagerOrAbove.value
    ? [{ label: t('event.details'), value: 'details', slot: 'details' }]
    : []),
  { label: t('event.meals'), value: 'meals', slot: 'meals' },
  { label: t('event.tasks'), value: 'tasks', slot: 'tasks' },
])

const activeTab = ref<string | number>(tabs.value[0]?.value ?? 0)

watch(activeTab, (newVal) => {
  const tab = tabs.value.find(tb => tb.value === newVal)
  if (tab) {
    history.replaceState(null, '', `#${tab.value}`)
  }
})

onMounted(() => {
  if (tabs.value.some(tab => tab.value === route.hash.substring(1))) {
    activeTab.value = route.hash.substring(1)
  }
})

// === Template data ===
const { templates: mealTemplates, pending: mealTemplatesPending, refresh: refreshMealTemplates } = useSortedMealTemplates(eventId)
const { templates: taskTemplates, pending: taskTemplatesPending, refresh: refreshTaskTemplates } = useSortedTaskTemplates(eventId)

const { deleteTemplate: deleteMealTemplate } = useMealTemplateActions(eventId, refreshMealTemplates)
const { deleteTemplate: deleteTaskTemplate } = useTaskTemplateActions(eventId, refreshTaskTemplates)

// === Meal template modal ===
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

// === Task template modal ===
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

// === Delete confirms ===
const mealToDelete = ref<MealTemplate | null>(null)
const taskToDelete = ref<TaskTemplate | null>(null)

function onMealModalDelete(template: MealTemplate) {
  mealToDelete.value = template
  showMealModal.value = false
}
function onTaskModalDelete(template: TaskTemplate) {
  taskToDelete.value = template
  showTaskModal.value = false
}

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
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="event">
      <GsBreadcrumb
        :items="[
          { label: t('event.title'), to: '/app/events' },
          { label: event.name, to: `/app/events/${eventId}` },
          { label: t('common.edit') },
        ]"
      />

      <GsPageHeader :title="`${t('common.edit')} ${event.name}`" />

      <UTabs v-model="activeTab" :items="tabs">
        <template #details>
          <div class="mt-4">
            <UForm :state="form" class="max-w-lg space-y-5" @submit="onSubmit">
              <GsEventForm
                v-model:name="form.name"
                v-model:property-id="form.propertyId"
                v-model:start-date="form.startDate"
                v-model:end-date="form.endDate"
                :property-items="propertyItems"
                :property-locked="true"
                :errors="errors"
              />

              <p class="text-xs text-muted">{{ t('event.dateChangeHint') }}</p>

              <UFormField :label="t('common.notes')">
                <UTextarea v-model="form.notes" class="w-full" />
              </UFormField>

              <GsAttributeField v-model="form.attributes" />

              <GsFormFooter
                submit-type="submit"
                :submit-label="t('common.save')"
                :loading="saving"
                :cancel-to="`/app/events/${eventId}`"
              >
                <template #delete>
                  <UButton
                    color="error"
                    variant="ghost"
                    icon="i-heroicons-trash"
                    :disabled="saving"
                    @click="showDeleteConfirm = true"
                  >
                    {{ t('event.deleteTitle') }}
                  </UButton>
                </template>
              </GsFormFooter>
            </UForm>
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
              <GsMealTemplateCard
                v-for="template in mealTemplates"
                :key="template.id"
                :name="template.name"
                :meal-types="template.mealTypes"
                :start-date="template.startDate"
                :end-date="template.endDate"
                :notes="template.notes"
                :attributes="template.attributes"
              >
                <template #actions>
                  <GsRoleGate min-role="Coordinator">
                    <UButton
                      variant="ghost"
                      size="xs"
                      icon="i-heroicons-pencil"
                      :aria-label="t('common.edit')"
                      @click="openEditMeal(template)"
                    />
                  </GsRoleGate>
                </template>

                <GsTemplatePlanList :event-id="eventId" :template-id="template.id" kind="meal" />
              </GsMealTemplateCard>
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
            <div v-else class="space-y-3">
              <GsTaskTemplateCard
                v-for="template in taskTemplates"
                :key="template.id"
                :name="template.name"
                :time-slots="template.timeSlots"
                :start-date="template.startDate"
                :end-date="template.endDate"
                :minimum-assignees="template.minimumAssignees"
                :notes="template.notes"
                :attributes="template.attributes"
              >
                <template #actions>
                  <GsRoleGate min-role="Coordinator">
                    <UButton
                      variant="ghost"
                      size="xs"
                      icon="i-heroicons-pencil"
                      :aria-label="t('common.edit')"
                      @click="openEditTask(template)"
                    />
                  </GsRoleGate>
                </template>

                <GsTemplatePlanList :event-id="eventId" :template-id="template.id" kind="task" />
              </GsTaskTemplateCard>
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

    <GsConfirmModal
      v-model:open="showDeleteConfirm"
      :title="t('event.deleteTitle')"
      :description="t('event.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDelete"
    />

    <GsMealTemplateModal
      v-model:open="showMealModal"
      :event-id="eventId"
      :event-start="form.startDate"
      :event-end="form.endDate"
      :template="editingMealTemplate"
      :refresh="refreshMealTemplates"
      :refresh-tasks="refreshTaskTemplates"
      @delete="onMealModalDelete"
    />
    <GsTaskTemplateModal
      v-model:open="showTaskModal"
      :event-id="eventId"
      :event-start="form.startDate"
      :event-end="form.endDate"
      :template="editingTaskTemplate"
      :refresh="refreshTaskTemplates"
      @delete="onTaskModalDelete"
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
  </div>
</template>

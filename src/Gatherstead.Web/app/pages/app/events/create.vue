<script setup lang="ts">
import type { MealTemplateDraft } from '~/components/GsMealTemplateModal.vue'
import type { TaskTemplateDraft } from '~/components/GsTaskTemplateModal.vue'
import type { AttributeWriteEntry } from '~/repositories/types'
import { useProperties } from '~/composables/useProperties'
import { useMealTemplateActions } from '~/composables/useMealPlans'
import { useTaskTemplateActions } from '~/composables/useTaskTemplates'
import { cleanAttributeWriteEntries, hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { properties, pending: propertiesPending } = useProperties()
const { refresh } = useEvents()
const { updating, createEvent } = useEventActions(refresh)

const saving = computed(() => updating.value.includes('new'))

const form = reactive({
  propertyId: '',
  name: '',
  startDate: '',
  endDate: '',
  notes: '',
  attributes: [] as AttributeWriteEntry[],
})

const errors = reactive({
  propertyId: '',
  name: '',
  dates: '',
})

const propertyItems = computed(() =>
  properties.value.map(p => ({ label: p.name, value: p.id })),
)

watchEffect(() => {
  const first = properties.value[0]
  if (!form.propertyId && first) form.propertyId = first.id
})

function validate(): boolean {
  errors.propertyId = form.propertyId ? '' : t('validation.required', { field: t('property.title') })
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('event.name') })
  errors.dates = ''
  if (!form.startDate || !form.endDate) {
    errors.dates = t('validation.required', { field: t('event.dateRangeLabel') })
  }
  else if (form.endDate < form.startDate) {
    errors.dates = t('event.endBeforeStart')
  }
  return !errors.propertyId && !errors.name && !errors.dates && !hasIncompleteAttributeRows(form.attributes)
}

// === Template drafts ===
const mealDrafts = ref<MealTemplateDraft[]>([])
const taskDrafts = ref<TaskTemplateDraft[]>([])

const showMealModal = ref(false)
const showTaskModal = ref(false)

function addMealDraft(draft: MealTemplateDraft) {
  mealDrafts.value.push(draft)
}
function addTaskDraft(draft: TaskTemplateDraft) {
  taskDrafts.value.push(draft)
}
function removeMealDraft(index: number) {
  mealDrafts.value.splice(index, 1)
}
function removeTaskDraft(index: number) {
  taskDrafts.value.splice(index, 1)
}

// Temporary event-id ref used to persist drafts after creation
const newEventId = ref('')
const mealActions = useMealTemplateActions(newEventId, async () => {})
const taskActions = useTaskTemplateActions(newEventId, async () => {})

async function onSubmit() {
  if (!validate()) return
  const created = await createEvent(
    form.propertyId, form.name.trim(), form.startDate, form.endDate,
    form.notes.trim() || null, cleanAttributeWriteEntries(form.attributes),
  )
  if (!created) return

  newEventId.value = created.id

  await Promise.all([
    ...mealDrafts.value.map(d =>
      mealActions.createTemplate(d.name, d.mealTypes, d.startDate, d.endDate, d.notes, d.attributes, d.createMatchingTask),
    ),
    ...taskDrafts.value.map(d =>
      taskActions.createTemplate(d.name, d.timeSlots, d.startDate, d.endDate, d.minimumAssignees, d.notes, d.attributes),
    ),
  ])

  await navigateTo(`/app/events/${created.id}`)
}
</script>

<template>
  <div>
    <GsBreadcrumb
      :items="[
        { label: t('event.title'), to: '/app/events' },
        { label: t('event.createTitle') },
      ]"
    />

    <GsPageHeader :title="t('event.createTitle')" />

    <div v-if="!propertiesPending && !properties.length">
      <GsEmptyState
        icon="i-heroicons-building-office-2"
        :title="t('event.noPropertiesTitle')"
        :description="t('event.noPropertiesHint')"
      >
        <UButton to="/app/properties" icon="i-heroicons-building-office-2">
          {{ t('property.title') }}
        </UButton>
      </GsEmptyState>
    </div>

    <template v-else>
      <UForm :state="form" class="max-w-lg space-y-5" @submit="onSubmit">
        <GsEventForm
          v-model:name="form.name"
          v-model:property-id="form.propertyId"
          v-model:start-date="form.startDate"
          v-model:end-date="form.endDate"
          :property-items="propertyItems"
          :errors="errors"
        />

        <UFormField :label="t('common.notes')">
          <UTextarea v-model="form.notes" class="w-full" />
        </UFormField>

        <GsAttributeField v-model="form.attributes" />

        <div class="flex items-center gap-3 pt-2">
          <UButton type="submit" :loading="saving">
            {{ t('common.create') }}
          </UButton>
          <UButton variant="ghost" to="/app/events">
            {{ t('common.cancel') }}
          </UButton>
        </div>
      </UForm>

      <div class="mt-10 pt-6 border-t border-default max-w-lg">
        <p class="font-semibold mb-1">{{ t('event.templateDraftsTitle') }}</p>
        <p class="text-sm text-muted mb-4">{{ t('event.templateDraftsHint') }}</p>

        <!-- Meal drafts -->
        <div class="mb-6">
          <div class="flex justify-between items-center mb-3">
            <p class="text-sm font-medium">{{ t('event.meals') }}</p>
            <UButton icon="i-heroicons-plus" size="xs" variant="ghost" @click="showMealModal = true">
              {{ t('event.meal.addTemplate') }}
            </UButton>
          </div>
          <div v-if="mealDrafts.length" class="space-y-2">
            <GsMealTemplateCard
              v-for="(draft, i) in mealDrafts"
              :key="i"
              :name="draft.name"
              :meal-types="draft.mealTypes"
              :start-date="draft.startDate"
              :end-date="draft.endDate"
              :notes="draft.notes"
            >
              <template #actions>
                <UButton
                  color="error"
                  variant="ghost"
                  size="xs"
                  icon="i-heroicons-trash"
                  :aria-label="t('common.delete')"
                  @click="removeMealDraft(i)"
                />
              </template>
            </GsMealTemplateCard>
          </div>
        </div>

        <!-- Task drafts -->
        <div>
          <div class="flex justify-between items-center mb-3">
            <p class="text-sm font-medium">{{ t('event.tasks') }}</p>
            <UButton icon="i-heroicons-plus" size="xs" variant="ghost" @click="showTaskModal = true">
              {{ t('event.task.addTemplate') }}
            </UButton>
          </div>
          <div v-if="taskDrafts.length" class="space-y-2">
            <GsTaskTemplateCard
              v-for="(draft, i) in taskDrafts"
              :key="i"
              :name="draft.name"
              :time-slots="draft.timeSlots"
              :start-date="draft.startDate"
              :end-date="draft.endDate"
              :minimum-assignees="draft.minimumAssignees"
              :notes="draft.notes"
            >
              <template #actions>
                <UButton
                  color="error"
                  variant="ghost"
                  size="xs"
                  icon="i-heroicons-trash"
                  :aria-label="t('common.delete')"
                  @click="removeTaskDraft(i)"
                />
              </template>
            </GsTaskTemplateCard>
          </div>
        </div>
      </div>
    </template>

    <GsMealTemplateModal
      v-model:open="showMealModal"
      draft-mode
      @save="addMealDraft"
    />
    <GsTaskTemplateModal
      v-model:open="showTaskModal"
      draft-mode
      @save="addTaskDraft"
    />
  </div>
</template>

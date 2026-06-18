<script setup lang="ts">
import type { MealTemplate, MealType, AttributeWriteEntry } from '~/repositories/types'
import { ALL_MEAL_TYPES, MEAL_TYPE_FLAGS, mealTypesFromFlags } from '~/repositories/types'
import { useMealTemplateActions } from '~/composables/useMealPlans'
import { toAttributeWriteEntries, cleanAttributeWriteEntries, hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'

export interface MealTemplateDraft {
  name: string
  mealTypes: number
  startDate: string | null
  endDate: string | null
  notes: string | null
  attributes: AttributeWriteEntry[]
  createMatchingTask: boolean
}

const props = defineProps<{
  eventId?: string
  refresh?: () => Promise<void>
  refreshTasks?: () => Promise<void>
  template?: MealTemplate | null
  draftMode?: boolean
}>()

const emit = defineEmits<{
  save: [MealTemplateDraft]
  delete: [template: MealTemplate]
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const eventId = computed(() => props.eventId ?? '')
const templateActions = props.draftMode ? null : useMealTemplateActions(eventId, props.refresh ?? (() => Promise.resolve()))
const { updating, createTemplate, updateTemplate } = templateActions ?? { updating: ref<string[]>([]), createTemplate: async () => false, updateTemplate: async () => false }

const isEdit = computed(() => !!props.template)
const saving = computed(() => updating.value.includes(props.template?.id ?? 'new'))

const form = reactive({
  name: '',
  mealTypes: [] as MealType[],
  useSubRange: false,
  startDate: '',
  endDate: '',
  notes: '',
  attributes: [] as AttributeWriteEntry[],
  createMatchingTask: false,
})

const errors = reactive({ name: '', mealTypes: '', dates: '' })

function reset() {
  const tpl = props.template
  form.name = tpl?.name ?? ''
  form.mealTypes = tpl ? mealTypesFromFlags(tpl.mealTypes) : ['Breakfast', 'Lunch', 'Dinner']
  form.useSubRange = !!(tpl?.startDate && tpl?.endDate)
  form.startDate = tpl?.startDate ?? ''
  form.endDate = tpl?.endDate ?? ''
  form.notes = tpl?.notes ?? ''
  form.attributes = toAttributeWriteEntries(tpl?.attributes)
  form.createMatchingTask = false
  errors.name = ''
  errors.mealTypes = ''
  errors.dates = ''
}

watch(open, (isOpen) => {
  if (isOpen) reset()
})

function toggleMealType(type: MealType) {
  const idx = form.mealTypes.indexOf(type)
  if (idx >= 0) form.mealTypes.splice(idx, 1)
  else form.mealTypes.push(type)
}

function validate(): boolean {
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('event.meal.templateName') })
  errors.mealTypes = form.mealTypes.length ? '' : t('event.meal.selectMealType')
  errors.dates = ''
  if (form.useSubRange) {
    if (!form.startDate || !form.endDate) errors.dates = t('validation.required', { field: t('event.meal.dateRangeLabel') })
    else if (form.endDate < form.startDate) errors.dates = t('event.endBeforeStart')
  }
  return !errors.name && !errors.mealTypes && !errors.dates && !hasIncompleteAttributeRows(form.attributes)
}

async function submit() {
  if (!validate()) return
  const flags = form.mealTypes.reduce((acc, mt) => acc | MEAL_TYPE_FLAGS[mt], 0)
  const start = form.useSubRange ? form.startDate : null
  const end = form.useSubRange ? form.endDate : null
  const notes = form.notes.trim() || null
  const attributes = cleanAttributeWriteEntries(form.attributes)

  if (props.draftMode) {
    emit('save', { name: form.name.trim(), mealTypes: flags, startDate: start, endDate: end, notes, attributes, createMatchingTask: form.createMatchingTask })
    open.value = false
    return
  }

  const ok = (isEdit.value && props.template)
    ? await updateTemplate(props.template.id, form.name.trim(), flags, start, end, notes, attributes)
    : await createTemplate(form.name.trim(), flags, start, end, notes, attributes, form.createMatchingTask)

  if (!ok) return
  if (form.createMatchingTask) await props.refreshTasks?.()
  open.value = false
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="isEdit ? t('event.meal.editTemplate') : t('event.meal.addTemplate')"
  >
    <template #body>
      <div class="space-y-5">
        <UFormField :label="t('event.meal.templateName')" :error="errors.name || undefined" required>
          <UInput v-model="form.name" :placeholder="t('event.meal.templateNamePlaceholder')" class="w-full" />
        </UFormField>

        <UFormField :label="t('event.meal.mealTypes')" :error="errors.mealTypes || undefined" required>
          <div class="flex flex-wrap gap-4">
            <UCheckbox
              v-for="type in ALL_MEAL_TYPES"
              :key="type"
              :model-value="form.mealTypes.includes(type)"
              :label="t(`event.meal.${type.toLowerCase()}`)"
              @update:model-value="toggleMealType(type)"
            />
          </div>
        </UFormField>

        <GsTemplateDateRangeField
          v-model:use-sub-range="form.useSubRange"
          v-model:start-date="form.startDate"
          v-model:end-date="form.endDate"
          :error="errors.dates || undefined"
        />

        <UFormField :label="t('common.notes')">
          <UTextarea v-model="form.notes" class="w-full" />
        </UFormField>

        <GsAttributeField v-model="form.attributes" />

        <UFormField v-if="!isEdit">
          <UCheckbox v-model="form.createMatchingTask" :label="t('event.meal.createMatchingTask')" />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <GsFormFooter
        :submit-label="isEdit ? t('common.save') : t('common.create')"
        :loading="saving"
        @submit="submit"
        @cancel="open = false"
      >
        <template v-if="isEdit && template" #delete>
          <UButton
            color="error"
            variant="ghost"
            icon="i-heroicons-trash"
            :disabled="saving"
            @click="emit('delete', template!)"
          >
            {{ t('event.meal.deleteTemplate') }}
          </UButton>
        </template>
      </GsFormFooter>
    </template>
  </UModal>
</template>

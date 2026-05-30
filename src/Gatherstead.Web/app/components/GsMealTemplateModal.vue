<script setup lang="ts">
import type { MealTemplate, MealType } from '~/repositories/types'
import { ALL_MEAL_TYPES, MEAL_TYPE_FLAGS, mealTypesFromFlags } from '~/repositories/types'
import { useMealTemplateActions } from '~/composables/useMealPlans'

const props = defineProps<{
  eventId: string
  refresh: () => Promise<void>
  template?: MealTemplate | null
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const eventId = computed(() => props.eventId)
const { updating, createTemplate, updateTemplate } = useMealTemplateActions(eventId, props.refresh)

const isEdit = computed(() => !!props.template)
const saving = computed(() => updating.value.includes(props.template?.id ?? 'new'))

const form = reactive({
  name: '',
  mealTypes: [] as MealType[],
  useSubRange: false,
  startDate: '',
  endDate: '',
  notes: '',
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
  return !errors.name && !errors.mealTypes && !errors.dates
}

async function submit() {
  if (!validate()) return
  const flags = form.mealTypes.reduce((acc, mt) => acc | MEAL_TYPE_FLAGS[mt], 0)
  const start = form.useSubRange ? form.startDate : null
  const end = form.useSubRange ? form.endDate : null
  const notes = form.notes.trim() || null

  const ok = (isEdit.value && props.template)
    ? await updateTemplate(props.template.id, form.name.trim(), flags, start, end, notes)
    : await createTemplate(form.name.trim(), flags, start, end, notes, form.createMatchingTask)

  // The action toasts on failure and refreshes the list on success (via the refresh prop);
  // keep the modal open with the user's input intact when the save did not go through.
  if (!ok) return
  open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6 space-y-5">
        <h3 class="text-lg font-semibold">
          {{ isEdit ? t('event.meal.editTemplate') : t('event.meal.addTemplate') }}
        </h3>

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

        <UFormField>
          <UCheckbox v-model="form.useSubRange" :label="t('event.meal.useSubRange')" />
        </UFormField>

        <div v-if="form.useSubRange" class="grid grid-cols-2 gap-4">
          <UFormField :label="t('event.startDate')">
            <UInput v-model="form.startDate" type="date" class="w-full" />
          </UFormField>
          <UFormField :label="t('event.endDate')">
            <UInput v-model="form.endDate" type="date" class="w-full" />
          </UFormField>
        </div>
        <p v-if="errors.dates" class="text-sm text-error -mt-2">{{ errors.dates }}</p>

        <UFormField :label="t('common.notes')">
          <UTextarea v-model="form.notes" class="w-full" />
        </UFormField>

        <UFormField v-if="!isEdit">
          <UCheckbox v-model="form.createMatchingTask" :label="t('event.meal.createMatchingTask')" />
        </UFormField>

        <div class="flex justify-end gap-3 pt-2">
          <UButton variant="ghost" :disabled="saving" @click="open = false">
            {{ t('common.cancel') }}
          </UButton>
          <UButton :loading="saving" @click="submit">
            {{ isEdit ? t('common.save') : t('common.create') }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>

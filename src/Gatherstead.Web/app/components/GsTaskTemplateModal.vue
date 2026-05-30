<script setup lang="ts">
import type { TaskTemplate, TaskTimeSlot } from '~/repositories/types'
import { ALL_TASK_SLOTS, TASK_SLOT_FLAGS, taskSlotsFromFlags } from '~/repositories/types'
import { useTaskTemplateActions } from '~/composables/useTaskTemplates'

const props = defineProps<{
  eventId: string
  refresh: () => Promise<void>
  template?: TaskTemplate | null
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const eventId = computed(() => props.eventId)
const { updating, createTemplate, updateTemplate } = useTaskTemplateActions(eventId, props.refresh)

const isEdit = computed(() => !!props.template)
const saving = computed(() => updating.value.includes(props.template?.id ?? 'new'))

const form = reactive({
  name: '',
  timeSlots: [] as TaskTimeSlot[],
  minimumAssignees: '',
  useSubRange: false,
  startDate: '',
  endDate: '',
  notes: '',
})

const errors = reactive({ name: '', timeSlots: '', dates: '' })

function reset() {
  const tpl = props.template
  form.name = tpl?.name ?? ''
  form.timeSlots = tpl ? taskSlotsFromFlags(tpl.timeSlots) : ['Anytime']
  form.minimumAssignees = tpl?.minimumAssignees != null ? String(tpl.minimumAssignees) : ''
  form.useSubRange = !!(tpl?.startDate && tpl?.endDate)
  form.startDate = tpl?.startDate ?? ''
  form.endDate = tpl?.endDate ?? ''
  form.notes = tpl?.notes ?? ''
  errors.name = ''
  errors.timeSlots = ''
  errors.dates = ''
}

watch(open, (isOpen) => {
  if (isOpen) reset()
})

function toggleSlot(slot: TaskTimeSlot) {
  const idx = form.timeSlots.indexOf(slot)
  if (idx >= 0) form.timeSlots.splice(idx, 1)
  else form.timeSlots.push(slot)
}

function validate(): boolean {
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('event.task.templateName') })
  errors.timeSlots = form.timeSlots.length ? '' : t('event.task.selectTimeSlot')
  errors.dates = ''
  if (form.useSubRange) {
    if (!form.startDate || !form.endDate) errors.dates = t('validation.required', { field: t('event.meal.dateRangeLabel') })
    else if (form.endDate < form.startDate) errors.dates = t('event.endBeforeStart')
  }
  return !errors.name && !errors.timeSlots && !errors.dates
}

async function submit() {
  if (!validate()) return
  const flags = form.timeSlots.reduce((acc, s) => acc | TASK_SLOT_FLAGS[s], 0)
  const start = form.useSubRange ? form.startDate : null
  const end = form.useSubRange ? form.endDate : null
  const min = form.minimumAssignees === '' ? null : Number(form.minimumAssignees)
  const notes = form.notes.trim() || null

  const ok = (isEdit.value && props.template)
    ? await updateTemplate(props.template.id, form.name.trim(), flags, start, end, min, notes)
    : await createTemplate(form.name.trim(), flags, start, end, min, notes)

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
          {{ isEdit ? t('event.task.editTemplate') : t('event.task.addTemplate') }}
        </h3>

        <UFormField :label="t('event.task.templateName')" :error="errors.name || undefined" required>
          <UInput v-model="form.name" :placeholder="t('event.task.templateNamePlaceholder')" class="w-full" />
        </UFormField>

        <UFormField :label="t('event.task.timeSlots')" :error="errors.timeSlots || undefined" required>
          <div class="flex flex-wrap gap-4">
            <UCheckbox
              v-for="slot in ALL_TASK_SLOTS"
              :key="slot"
              :model-value="form.timeSlots.includes(slot)"
              :label="t(`event.task.${slot.toLowerCase()}`)"
              @update:model-value="toggleSlot(slot)"
            />
          </div>
        </UFormField>

        <UFormField :label="t('event.task.minimumAssigneesLabel')">
          <UInput v-model="form.minimumAssignees" type="number" min="0" class="w-full" />
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

<script setup lang="ts">
import { useHouseholdActions } from '~/composables/useHouseholds'
import { toAttributeWriteEntries, cleanAttributeWriteEntries, hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'
import type { HouseholdSummary, AttributeWriteEntry } from '~/repositories/types'

// Create + edit in one component. Passing `household` switches to edit mode; omitting it creates.
const props = defineProps<{
  refresh: () => Promise<void>
  household?: HouseholdSummary | null
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const { updating, createHousehold, updateHousehold } = useHouseholdActions(props.refresh)

const isEdit = computed(() => !!props.household)
const saving = computed(() => updating.value.includes(props.household?.id ?? 'new'))

const form = reactive({
  name: '',
  notes: '',
  attributes: [] as AttributeWriteEntry[],
})
const nameError = ref('')

watch(open, (isOpen) => {
  if (isOpen) {
    form.name = props.household?.name ?? ''
    form.notes = props.household?.notes ?? ''
    form.attributes = toAttributeWriteEntries(props.household?.attributes)
    nameError.value = ''
  }
})

async function submit() {
  nameError.value = ''
  const trimmed = form.name.trim()
  if (!trimmed) {
    nameError.value = t('validation.required', { field: t('household.name') })
    return
  }
  // A row with a value but no label would be silently dropped — block save so the editor's
  // inline warning prompts the user to add a label or remove it with the delete button.
  if (hasIncompleteAttributeRows(form.attributes)) return
  const notes = form.notes.trim() || null
  const attributes = cleanAttributeWriteEntries(form.attributes)
  const ok = (isEdit.value && props.household)
    ? await updateHousehold(props.household.id, trimmed, notes, attributes)
    : await createHousehold(trimmed, notes, attributes)
  if (ok) open.value = false
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="isEdit ? t('household.editTitle') : t('household.createTitle')"
  >
    <template #body>
      <div class="space-y-5">
        <UFormField :label="t('household.name')" :error="nameError || undefined" required>
          <UInput
            v-model="form.name"
            :placeholder="t('household.name')"
            class="w-full"
            @input="nameError = ''"
          />
        </UFormField>
        <UFormField :label="t('common.notes')">
          <UTextarea v-model="form.notes" class="w-full" />
        </UFormField>
        <GsAttributeField v-model="form.attributes" />
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-3 w-full">
        <UButton variant="ghost" :disabled="saving" @click="open = false">
          {{ t('common.cancel') }}
        </UButton>
        <UButton :loading="saving" @click="submit">
          {{ isEdit ? t('common.save') : t('common.create') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

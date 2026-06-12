<script setup lang="ts">
import { usePropertyActions } from '~/composables/useProperties'
import { toAttributeWriteEntries, cleanAttributeWriteEntries, hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'
import type { PropertySummary, AttributeWriteEntry } from '~/repositories/types'

// Create + edit in one component. Passing `property` switches to edit mode; omitting it creates.
const props = defineProps<{
  refresh: () => Promise<void>
  property?: PropertySummary | null
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const { updating, createProperty, updateProperty } = usePropertyActions(props.refresh)

const isEdit = computed(() => !!props.property)
const saving = computed(() => updating.value.includes(props.property?.id ?? 'new'))

const form = reactive({
  name: '',
  notes: '',
  attributes: [] as AttributeWriteEntry[],
})
const nameError = ref('')

watch(open, (isOpen) => {
  if (isOpen) {
    form.name = props.property?.name ?? ''
    form.notes = props.property?.notes ?? ''
    form.attributes = toAttributeWriteEntries(props.property?.attributes)
    nameError.value = ''
  }
})

async function submit() {
  nameError.value = ''
  const trimmed = form.name.trim()
  if (!trimmed) {
    nameError.value = t('validation.required', { field: t('property.name') })
    return
  }
  // A row with a value but no label would be silently dropped — block save so the editor's
  // inline warning prompts the user to add a label or remove it with the delete button.
  if (hasIncompleteAttributeRows(form.attributes)) return
  const notes = form.notes.trim() || null
  const attributes = cleanAttributeWriteEntries(form.attributes)
  const ok = (isEdit.value && props.property)
    ? await updateProperty(props.property.id, trimmed, notes, attributes)
    : await createProperty(trimmed, notes, attributes)
  if (ok) open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6 space-y-5">
        <h3 class="text-lg font-semibold">
          {{ isEdit ? t('property.editTitle') : t('property.createTitle') }}
        </h3>
        <UFormField :label="t('property.name')" :error="nameError || undefined" required>
          <UInput
            v-model="form.name"
            :placeholder="t('property.name')"
            class="w-full"
            @input="nameError = ''"
          />
        </UFormField>
        <UFormField :label="t('common.notes')">
          <UTextarea v-model="form.notes" class="w-full" />
        </UFormField>
        <GsAttributeField v-model="form.attributes" />
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

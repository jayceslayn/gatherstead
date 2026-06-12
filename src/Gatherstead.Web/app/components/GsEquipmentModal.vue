<script setup lang="ts">
import type { EquipmentSummary, AttributeWriteEntry } from '~/repositories/types'
import { useEquipmentActions } from '~/composables/useEquipment'
import { hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'

// Create + edit in one component. Passing `equipment` switches to edit mode; omitting it creates.
const props = defineProps<{
  refresh: () => Promise<void>
  equipment?: EquipmentSummary | null
  propertyItems: { label: string, value: string }[]
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const { updating, createEquipment, updateEquipment } = useEquipmentActions(props.refresh)

const isEdit = computed(() => !!props.equipment)
const saving = computed(() => updating.value.includes(props.equipment?.id ?? 'new'))

// USelect (Reka UI) rejects empty-string item values, so use a sentinel for "no property".
const NO_PROPERTY = '__none__'

const form = reactive({
  name: '',
  propertyId: NO_PROPERTY,
  notes: '',
  attributes: [] as AttributeWriteEntry[],
})
const errors = reactive({ name: '' })

// Property is optional — equipment may be group-wide or scoped to a property.
const propertyOptions = computed(() => [
  { label: t('equipment.noProperty'), value: NO_PROPERTY },
  ...props.propertyItems,
])

function reset() {
  const e = props.equipment
  form.name = e?.name ?? ''
  form.propertyId = e?.propertyId ?? NO_PROPERTY
  form.notes = e?.notes ?? ''
  form.attributes = (e?.attributes ?? []).map(a => ({
    key: a.key,
    value: a.value,
    tenantMinRole: a.tenantMinRole,
    householdMinRole: a.householdMinRole,
  }))
  errors.name = ''
}

watch(open, (isOpen) => {
  if (isOpen) reset()
})

function validate(): boolean {
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('equipment.name') })
  return !errors.name && !hasIncompleteAttributeRows(form.attributes)
}

async function submit() {
  if (!validate()) return
  // Only persist attribute rows that actually carry a label.
  const attributes = form.attributes.filter(a => (a.key ?? '').trim())
  const notes = form.notes.trim() || null
  const propertyId = form.propertyId === NO_PROPERTY ? null : form.propertyId

  const ok = (isEdit.value && props.equipment)
    ? await updateEquipment(props.equipment.id, form.name.trim(), propertyId, notes, attributes)
    : await createEquipment(form.name.trim(), propertyId, notes, attributes)

  if (ok) open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6 space-y-5">
        <h3 class="text-lg font-semibold">
          {{ isEdit ? t('equipment.editTitle') : t('equipment.createTitle') }}
        </h3>

        <UFormField :label="t('equipment.name')" :error="errors.name || undefined" required>
          <UInput v-model="form.name" :placeholder="t('equipment.namePlaceholder')" class="w-full" />
        </UFormField>

        <UFormField :label="t('equipment.property')">
          <USelect
            v-model="form.propertyId"
            :items="propertyOptions"
            class="w-full"
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

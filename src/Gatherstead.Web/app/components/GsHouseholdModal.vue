<script setup lang="ts">
import { useHouseholdActions } from '~/composables/useHouseholds'
import type { HouseholdSummary } from '~/repositories/types'

// Create + edit in one component. Passing `household` switches to edit mode (rename); omitting it
// creates a new household. Mirrors the shared-modal approach used by the meal/task template modals.
const props = defineProps<{
  refresh: () => Promise<void>
  household?: HouseholdSummary | null
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const { updating, createHousehold, updateHousehold } = useHouseholdActions(props.refresh)

const isEdit = computed(() => !!props.household)
const name = ref('')
const nameError = ref('')
const saving = computed(() => updating.value.includes(props.household?.id ?? 'new'))

watch(open, (isOpen) => {
  if (isOpen) {
    name.value = props.household?.name ?? ''
    nameError.value = ''
  }
})

async function submit() {
  nameError.value = ''
  const trimmed = name.value.trim()
  if (!trimmed) {
    nameError.value = t('validation.required', { field: t('household.name') })
    return
  }
  const ok = (isEdit.value && props.household)
    ? await updateHousehold(props.household.id, trimmed)
    : await createHousehold(trimmed)
  if (ok) open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6">
        <h3 class="text-lg font-semibold mb-4">
          {{ isEdit ? t('household.editTitle') : t('household.createTitle') }}
        </h3>
        <UFormField :label="t('household.name')" :error="nameError || undefined">
          <UInput
            v-model="name"
            :placeholder="t('household.name')"
            @keydown.enter="submit"
            @input="nameError = ''"
          />
        </UFormField>
        <div class="flex justify-end gap-3 mt-6">
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

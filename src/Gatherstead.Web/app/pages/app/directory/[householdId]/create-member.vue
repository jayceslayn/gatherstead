<script setup lang="ts">
import type { AttributeWriteEntry } from '~/repositories/types'
import { useHousehold } from '~/composables/useHouseholds'
import { useHouseholdMembers, useHouseholdMemberActions } from '~/composables/useHouseholdMembers'
import { cleanAttributeWriteEntries, hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()

const householdId = computed(() => route.params.householdId as string)

const { household } = useHousehold(householdId)
const { refresh } = useHouseholdMembers(householdId)
const { updating, createMember } = useHouseholdMemberActions(householdId, refresh)

const saving = computed(() => updating.value.includes('new'))

const form = reactive({
  name: '',
  ageBand: '',
  birthDate: '',
  dietaryNotes: '',
  dietaryTags: [] as string[],
  notes: '',
  attributes: [] as AttributeWriteEntry[],
})

const nameError = ref('')

async function onSubmit() {
  nameError.value = form.name.trim() ? '' : t('validation.required', { field: t('member.name') })
  if (nameError.value) return
  // A row with a value but no label would be silently dropped — block save so the editor's
  // inline warning prompts the user to add a label or remove it with the delete button.
  if (hasIncompleteAttributeRows(form.attributes)) return

  const created = await createMember(
    form.name.trim(),
    form.ageBand.trim() || null,
    form.birthDate || null,
    form.dietaryNotes.trim() || null,
    form.notes.trim() || null,
    form.dietaryTags,
    cleanAttributeWriteEntries(form.attributes),
  )

  if (created) {
    await navigateTo(`/app/directory/${householdId.value}/${created.id}`)
  }
}
</script>

<template>
  <div>
    <GsBreadcrumb
      :items="[
        { label: t('household.title'), to: '/app/directory' },
        { label: household?.name ?? '…', to: `/app/directory/${householdId}` },
        { label: t('member.createMember') },
      ]"
    />

    <GsPageHeader :title="t('member.createMember')" />

    <GsMemberForm
      v-model:name="form.name"
      v-model:age-band="form.ageBand"
      v-model:birth-date="form.birthDate"
      v-model:dietary-notes="form.dietaryNotes"
      v-model:dietary-tags="form.dietaryTags"
      v-model:notes="form.notes"
      v-model:attributes="form.attributes"
      :name-error="nameError"
      :loading="saving"
      :cancel-to="`/app/directory/${householdId}`"
      :submit-label="t('common.create')"
      @submit="onSubmit"
      @clear-name-error="nameError = ''"
    />
  </div>
</template>

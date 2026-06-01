<script setup lang="ts">
import { useHousehold } from '~/composables/useHouseholds'
import { useHouseholdMembers, useHouseholdMemberActions } from '~/composables/useHouseholdMembers'

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
  isAdult: true,
  ageBand: '',
  birthDate: '',
  dietaryNotes: '',
  dietaryTags: [] as string[],
})

const nameError = ref('')

async function onSubmit() {
  nameError.value = form.name.trim() ? '' : t('validation.required', { field: t('member.name') })
  if (nameError.value) return

  const created = await createMember(
    form.name.trim(),
    form.isAdult,
    form.ageBand.trim() || null,
    form.birthDate || null,
    form.dietaryNotes.trim() || null,
    form.dietaryTags,
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
      v-model:is-adult="form.isAdult"
      v-model:age-band="form.ageBand"
      v-model:birth-date="form.birthDate"
      v-model:dietary-notes="form.dietaryNotes"
      v-model:dietary-tags="form.dietaryTags"
      :name-error="nameError"
      :loading="saving"
      :cancel-to="`/app/directory/${householdId}`"
      :submit-label="t('common.create')"
      @submit="onSubmit"
      @clear-name-error="nameError = ''"
    />
  </div>
</template>

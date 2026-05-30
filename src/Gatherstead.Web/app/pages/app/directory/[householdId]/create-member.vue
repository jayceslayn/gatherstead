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

interface MemberForm {
  name: string
  isAdult: boolean
  ageBand: string
  birthDate: string
  dietaryNotes: string
  dietaryTagsInput: string
}

const form = reactive<MemberForm>({
  name: '',
  isAdult: true,
  ageBand: '',
  birthDate: '',
  dietaryNotes: '',
  dietaryTagsInput: '',
})

const nameError = ref('')

async function onSubmit() {
  nameError.value = form.name.trim() ? '' : t('validation.required', { field: t('member.name') })
  if (nameError.value) return

  const dietaryTags = form.dietaryTagsInput
    .split(',')
    .map(s => s.trim())
    .filter(Boolean)

  const created = await createMember(
    form.name.trim(),
    form.isAdult,
    form.ageBand.trim() || null,
    form.birthDate || null,
    form.dietaryNotes.trim() || null,
    dietaryTags,
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

    <UForm :state="form" class="max-w-lg space-y-5" @submit="onSubmit">
      <UFormField :label="t('member.name')" name="name" :error="nameError || undefined" required>
        <UInput v-model="form.name" :placeholder="t('member.name')" required class="w-full" @input="nameError = ''" />
      </UFormField>

      <UFormField :label="t('member.isAdult')" name="isAdult">
        <UCheckbox v-model="form.isAdult" :label="t('member.adult')" />
      </UFormField>

      <UFormField :label="t('member.ageBand')" name="ageBand">
        <UInput v-model="form.ageBand" :placeholder="t('member.ageBandPlaceholder')" class="w-full" />
      </UFormField>

      <UFormField :label="t('member.birthDate')" name="birthDate">
        <UInput v-model="form.birthDate" type="date" class="w-full" />
      </UFormField>

      <UFormField :label="t('member.dietaryNotes')" name="dietaryNotes">
        <UTextarea v-model="form.dietaryNotes" :placeholder="t('member.dietaryNotesPlaceholder')" class="w-full" />
      </UFormField>

      <UFormField :label="t('member.dietaryTags')" name="dietaryTags" :hint="t('member.dietaryTagsHint')">
        <UInput v-model="form.dietaryTagsInput" :placeholder="t('member.dietaryTagsPlaceholder')" class="w-full" />
      </UFormField>

      <div class="flex items-center gap-3 pt-2">
        <UButton type="submit" :loading="saving">
          {{ t('common.create') }}
        </UButton>
        <UButton
          variant="ghost"
          :to="`/app/directory/${householdId}`"
        >
          {{ t('common.cancel') }}
        </UButton>
      </div>
    </UForm>
  </div>
</template>

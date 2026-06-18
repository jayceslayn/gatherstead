<script setup lang="ts">
import type { HouseholdMember, AttributeWriteEntry } from '~/repositories/types'
import { useHousehold } from '~/composables/useHouseholds'
import { useMember, useHouseholdMembers, useHouseholdMemberActions } from '~/composables/useHouseholdMembers'
import { toAttributeWriteEntries, cleanAttributeWriteEntries, hasIncompleteAttributeRows } from '~/composables/useAttributeRoles'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()

const householdId = computed(() => route.params.householdId as string)
const memberId = computed(() => route.params.memberId as string)

const { household } = useHousehold(householdId)
const { member, pending } = useMember(householdId, memberId)
const { refresh } = useHouseholdMembers(householdId)
const { updating, updateMember, deleteMember } = useHouseholdMemberActions(householdId, refresh)

const saving = computed(() => updating.value.includes(memberId.value))
const deleting = computed(() => updating.value.includes(memberId.value))
const showDeleteConfirm = ref(false)

async function confirmDelete() {
  showDeleteConfirm.value = false
  await deleteMember(memberId.value)
  await navigateTo(`/app/directory/${householdId.value}`)
}

const form = reactive({
  name: '',
  isAdult: true,
  ageBand: '',
  birthDate: '',
  dietaryNotes: '',
  dietaryTags: [] as string[],
  attributes: [] as AttributeWriteEntry[],
})

const nameError = ref('')
const isDirty = ref(false)

watch(member, (val: HouseholdMember | null) => {
  if (!val) return
  form.name = val.name
  form.isAdult = val.isAdult
  form.ageBand = val.ageBand ?? ''
  form.birthDate = val.birthDate ?? ''
  form.dietaryNotes = val.dietaryNotes ?? ''
  form.dietaryTags = [...val.dietaryTags]
  form.attributes = toAttributeWriteEntries(val.attributes)
  // Reset after the form watcher's next-tick flush so pre-fill doesn't mark as dirty
  nextTick(() => { isDirty.value = false })
}, { immediate: true })

watch(form, () => { isDirty.value = true }, { deep: true })

onBeforeRouteLeave(() => {
  if (isDirty.value && !saving.value) {
    return confirm(t('common.unsavedChanges'))
  }
})

async function onSubmit() {
  nameError.value = form.name.trim() ? '' : t('validation.required', { field: t('member.name') })
  if (nameError.value) return
  // A row with a value but no label would be silently dropped — block save so the editor's
  // inline warning prompts the user to add a label or remove it with the delete button.
  if (hasIncompleteAttributeRows(form.attributes)) return

  const ok = await updateMember(
    memberId.value,
    form.name.trim(),
    form.isAdult,
    form.ageBand.trim() || null,
    form.birthDate || null,
    form.dietaryNotes.trim() || null,
    form.dietaryTags,
    cleanAttributeWriteEntries(form.attributes),
  )
  if (ok) {
    isDirty.value = false
    await navigateTo(`/app/directory/${householdId.value}/${memberId.value}`)
  }
}
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="member">
      <GsBreadcrumb
        :items="[
          { label: t('household.title'), to: '/app/directory' },
          { label: household?.name ?? '…', to: `/app/directory/${householdId}` },
          { label: member.name, to: `/app/directory/${householdId}/${memberId}` },
          { label: t('common.edit') },
        ]"
      />

      <GsPageHeader :title="`${t('common.edit')} ${member.name}`" />

      <GsMemberForm
        v-model:name="form.name"
        v-model:is-adult="form.isAdult"
        v-model:age-band="form.ageBand"
        v-model:birth-date="form.birthDate"
        v-model:dietary-notes="form.dietaryNotes"
        v-model:dietary-tags="form.dietaryTags"
        v-model:attributes="form.attributes"
        :name-error="nameError"
        :loading="saving"
        :cancel-to="`/app/directory/${householdId}/${memberId}`"
        :submit-label="t('common.save')"
        @submit="onSubmit"
        @clear-name-error="nameError = ''"
      >
        <template #delete>
          <GsRoleGate min-role="Manager">
            <UButton
              color="error"
              variant="ghost"
              icon="i-heroicons-trash"
              :loading="deleting"
              @click="showDeleteConfirm = true"
            >
              {{ t('member.deleteTitle') }}
            </UButton>
          </GsRoleGate>
        </template>
      </GsMemberForm>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />

    <GsConfirmModal
      v-model:open="showDeleteConfirm"
      :title="t('member.deleteTitle')"
      :description="t('member.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDelete"
    />
  </div>
</template>

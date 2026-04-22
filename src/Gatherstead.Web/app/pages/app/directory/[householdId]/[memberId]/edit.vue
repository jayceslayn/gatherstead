<script setup lang="ts">
import { useTenantStore } from '~/stores/tenant'
import type { HouseholdMember } from '~/composables/useHouseholdMembers'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const toast = useToast()
const tenantStore = useTenantStore()

const householdId = computed(() => route.params.householdId as string)
const memberId = computed(() => route.params.memberId as string)

const { household } = useHousehold(householdId)
const { member, pending } = useMember(householdId, memberId)

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

const isDirty = ref(false)
const saving = ref(false)

watch(member, (val: HouseholdMember | null) => {
  if (!val) return
  form.name = val.name
  form.isAdult = val.isAdult
  form.ageBand = val.ageBand ?? ''
  form.birthDate = val.birthDate ?? ''
  form.dietaryNotes = val.dietaryNotes ?? ''
  form.dietaryTagsInput = val.dietaryTags.join(', ')
  // Reset after the form watcher's next-tick flush so it doesn't mark as dirty on load
  nextTick(() => { isDirty.value = false })
}, { immediate: true })

watch(form, () => { isDirty.value = true }, { deep: true })

onBeforeRouteLeave(() => {
  if (isDirty.value && !saving.value) {
    return confirm(t('common.unsavedChanges'))
  }
})

async function onSubmit() {
  if (!form.name.trim()) return

  saving.value = true
  try {
    const dietaryTags = form.dietaryTagsInput
      .split(',')
      .map(s => s.trim())
      .filter(Boolean)

    await $fetch(
      `/api/proxy/tenants/${tenantStore.currentTenantId}/households/${householdId.value}/members/${memberId.value}`,
      {
        method: 'PUT',
        body: {
          name: form.name.trim(),
          isAdult: form.isAdult,
          ageBand: form.ageBand.trim() || null,
          birthDate: form.birthDate || null,
          dietaryNotes: form.dietaryNotes.trim() || null,
          dietaryTags,
        },
      },
    )

    isDirty.value = false
    toast.add({ title: t('member.savedSuccessfully'), color: 'success' })
    await navigateTo(`/app/directory/${householdId.value}/${memberId.value}`)
  }
  catch {
    toast.add({ title: t('common.error'), color: 'error' })
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="member">
      <GsBreadcrumb :items="[
        { label: t('household.title'), to: '/app/directory' },
        { label: household?.name ?? '…', to: `/app/directory/${householdId}` },
        { label: member.name, to: `/app/directory/${householdId}/${memberId}` },
        { label: t('common.edit') },
      ]" />

      <GsPageHeader :title="`${t('common.edit')} ${member.name}`" />

      <UForm :state="form" class="max-w-lg space-y-5" @submit="onSubmit">
        <UFormField :label="t('member.name')" name="name" required>
          <UInput v-model="form.name" :placeholder="t('member.name')" required class="w-full" />
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
            {{ t('common.save') }}
          </UButton>
          <UButton
            variant="ghost"
            :to="`/app/directory/${householdId}/${memberId}`"
          >
            {{ t('common.cancel') }}
          </UButton>
        </div>
      </UForm>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>

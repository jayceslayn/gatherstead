<script setup lang="ts">
import { useMe, useMeActions } from '~/composables/useMe'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { me, pending, refresh } = useMe()
const { saving, updateDisplayName } = useMeActions(refresh)

const displayName = ref('')
const nameError = ref('')

// Seed the editable field from the loaded profile (and on later refreshes).
watch(me, (value) => {
  displayName.value = value?.displayName ?? ''
}, { immediate: true })

async function onSubmit() {
  const trimmed = displayName.value.trim()
  if (!trimmed) {
    nameError.value = t('account.displayNameRequired')
    return
  }
  await updateDisplayName(trimmed)
}
</script>

<template>
  <div>
    <GsPageHeader :title="t('account.title')" />

    <USkeleton v-if="pending && !me" class="h-40 w-full max-w-lg" />

    <UForm
      v-else
      :state="{ displayName }"
      class="max-w-lg space-y-5"
      @submit="onSubmit"
    >
      <UFormField :label="t('account.email')" name="email">
        <UInput :model-value="me?.email ?? ''" disabled class="w-full" />
        <template #help>{{ t('account.emailHelp') }}</template>
      </UFormField>

      <UFormField :label="t('account.displayName')" name="displayName" :error="nameError || undefined" required>
        <UInput
          v-model="displayName"
          :placeholder="t('account.displayName')"
          required
          class="w-full"
          @input="nameError = ''"
        />
      </UFormField>

      <GsFormFooter
        submit-type="submit"
        :submit-label="t('common.save')"
        :loading="saving"
        cancel-to="/app"
      />
    </UForm>
  </div>
</template>

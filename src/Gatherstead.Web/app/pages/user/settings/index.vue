<script setup lang="ts">
import { useMe, useMeActions } from '~/composables/useMe'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const { me, pending, refresh } = useMe()
const { saving, updateDisplayName, deleting, deleteAccount } = useMeActions(refresh)
const isDemoMode = __DEMO_MODE__

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

// ── Delete account (type-to-confirm) ──────────────────────────────────────────
const confirmOpen = ref(false)
const confirmText = ref('')
const deleteError = ref('')
// Require typing the account email (locale-neutral and personal); fall back to a fixed token.
const requiredPhrase = computed(() => me.value?.email || 'DELETE')
const canConfirm = computed(() =>
  confirmText.value.trim().toLowerCase() === requiredPhrase.value.toLowerCase())

function openDelete() {
  confirmText.value = ''
  deleteError.value = ''
  confirmOpen.value = true
}

async function onConfirmDelete() {
  if (!canConfirm.value) return
  // On success the browser navigates away; on failure the dialog stays open with the reason
  // (e.g. sole owner of a shared group) shown inline.
  deleteError.value = (await deleteAccount()) ?? ''
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

    <section v-if="!isDemoMode && me" class="max-w-lg mt-12 rounded-lg border border-error/40 p-5">
      <h2 class="font-semibold text-error mb-1">{{ t('account.delete.title') }}</h2>
      <p class="text-sm text-neutral-600 dark:text-neutral-400 mb-4">{{ t('account.delete.description') }}</p>
      <UButton color="error" variant="soft" icon="i-heroicons-trash" @click="openDelete">
        {{ t('account.delete.button') }}
      </UButton>
    </section>

    <GsConfirmModal
      v-model:open="confirmOpen"
      :title="t('account.delete.confirmTitle')"
      :description="t('account.delete.confirmWarning')"
      :confirm-label="t('account.delete.confirmButton')"
      danger
      :loading="deleting"
      :confirm-disabled="!canConfirm"
      :close-on-confirm="false"
      @confirm="onConfirmDelete"
    >
      <div class="space-y-4 mb-6">
        <ul class="text-sm text-neutral-600 dark:text-neutral-400 list-disc pl-5 space-y-1">
          <li>{{ t('account.delete.bulletData') }}</li>
          <li>{{ t('account.delete.bulletGroups') }}</li>
          <li>{{ t('account.delete.bulletIrreversible') }}</li>
        </ul>
        <UFormField :label="t('account.delete.confirmPrompt', { phrase: requiredPhrase })">
          <UInput v-model="confirmText" class="w-full" autocomplete="off" spellcheck="false" />
        </UFormField>
        <p v-if="deleteError" class="text-sm text-error">{{ deleteError }}</p>
      </div>
    </GsConfirmModal>
  </div>
</template>

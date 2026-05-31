<script setup lang="ts">
import type { TenantRole, HouseholdRole } from '~/repositories/types'
import { useInvitationActions } from '~/composables/useTenantUsers'
import { useHouseholds } from '~/composables/useHouseholds'

const props = defineProps<{
  refresh?: () => Promise<void>
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const { households } = useHouseholds()
const { saving, invite } = useInvitationActions(props.refresh)

const ASSIGNABLE_ROLES: TenantRole[] = ['Manager', 'Coordinator', 'Member', 'Guest']

const form = reactive({
  email: '',
  role: 'Member' as TenantRole,
  householdId: '' as string,
  householdRole: 'Member' as HouseholdRole,
})
const emailError = ref('')

const roleItems = computed(() => ASSIGNABLE_ROLES.map(r => ({ label: t(`tenantUser.roles.${r}`), value: r })))
const householdItems = computed(() => [
  { label: t('tenantUser.invite.noHousehold'), value: '' },
  ...households.value.map(h => ({ label: h.name, value: h.id })),
])
const householdRoleItems = computed(() => [
  { label: t('tenantUser.householdRoles.Manager'), value: 'Manager' as HouseholdRole },
  { label: t('tenantUser.householdRoles.Member'), value: 'Member' as HouseholdRole },
])

watch(open, (isOpen) => {
  if (isOpen) {
    form.email = ''
    form.role = 'Member'
    form.householdId = ''
    form.householdRole = 'Member'
    emailError.value = ''
  }
})

function isValidEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
}

async function submit() {
  emailError.value = ''
  const email = form.email.trim()
  if (!isValidEmail(email)) {
    emailError.value = t('validation.invalidEmail')
    return
  }
  const ok = await invite(
    email,
    form.role,
    form.householdId || null,
    form.householdId ? form.householdRole : null,
  )
  if (ok) open.value = false
}
</script>

<template>
  <UModal v-model:open="open">
    <template #content>
      <div class="p-6 space-y-5">
        <h3 class="text-lg font-semibold">{{ t('tenantUser.invite.title') }}</h3>
        <p class="text-sm text-muted">{{ t('tenantUser.invite.hint') }}</p>

        <UFormField :label="t('tenantUser.invite.email')" :error="emailError || undefined" required>
          <UInput
            v-model="form.email"
            type="email"
            :placeholder="t('tenantUser.invite.emailPlaceholder')"
            class="w-full"
            @keydown.enter="submit"
          />
        </UFormField>

        <UFormField :label="t('tenantUser.role')" required>
          <USelect v-model="form.role" :items="roleItems" class="w-full" />
        </UFormField>

        <UFormField :label="t('tenantUser.invite.household')">
          <USelect v-model="form.householdId" :items="householdItems" class="w-full" />
        </UFormField>

        <UFormField v-if="form.householdId" :label="t('tenantUser.invite.householdRole')">
          <USelect v-model="form.householdRole" :items="householdRoleItems" class="w-full" />
        </UFormField>

        <div class="flex justify-end gap-3 pt-2">
          <UButton variant="ghost" :disabled="saving" @click="open = false">
            {{ t('common.cancel') }}
          </UButton>
          <UButton :loading="saving" @click="submit">
            {{ t('tenantUser.invite.send') }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>

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

// USelect (Reka UI) rejects empty-string item values, so use a sentinel for "no household".
const NO_HOUSEHOLD = '__none__'

const form = reactive({
  email: '',
  role: 'Member' as TenantRole,
  householdId: NO_HOUSEHOLD,
  householdRole: 'Member' as HouseholdRole,
})
const emailError = ref('')

const roleItems = computed(() => ASSIGNABLE_ROLES.map(r => ({ label: t(`tenantUser.roles.${r}`), value: r })))
const householdItems = computed(() => [
  { label: t('tenantUser.invite.noHousehold'), value: NO_HOUSEHOLD },
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
    form.householdId = NO_HOUSEHOLD
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
  const householdId = form.householdId === NO_HOUSEHOLD ? null : form.householdId
  const ok = await invite(
    email,
    form.role,
    householdId,
    householdId ? form.householdRole : null,
  )
  if (ok) open.value = false
}
</script>

<template>
  <UModal v-model:open="open" :title="t('tenantUser.invite.title')">
    <template #body>
      <div class="space-y-5">
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

        <UFormField v-if="form.householdId !== NO_HOUSEHOLD" :label="t('tenantUser.invite.householdRole')">
          <USelect v-model="form.householdRole" :items="householdRoleItems" class="w-full" />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <GsFormFooter
        :submit-label="t('tenantUser.invite.send')"
        :loading="saving"
        @submit="submit"
        @cancel="open = false"
      />
    </template>
  </UModal>
</template>

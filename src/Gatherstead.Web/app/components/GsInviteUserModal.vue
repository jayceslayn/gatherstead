<script setup lang="ts">
import type { TenantRole, HouseholdRole } from '~/repositories/types'
import { useInvitationActions } from '~/composables/useTenantUsers'
import { useHouseholds } from '~/composables/useHouseholds'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'

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
  // Optional household-access grants — a user can hold a role in multiple households.
  households: [] as { householdId: string, role: HouseholdRole }[],
})
const emailError = ref('')

const roleItems = computed(() => ASSIGNABLE_ROLES.map(r => ({ label: t(`tenantUser.roles.${r}`), value: r })))
const householdOptions = computed(() => households.value.map(h => ({ label: h.name, value: h.id })))
const hhRoleOptions = computed((): { label: string, value: HouseholdRole }[] => [
  { label: t('tenantUser.householdRoles.Manager'), value: 'Manager' },
  { label: t('tenantUser.householdRoles.Member'), value: 'Member' },
])

function householdName(id: string): string {
  return households.value.find(h => h.id === id)?.name ?? id
}

// ── Linked Member (optional) — mirrors the user-edit page's Linked Member card ──
// The household picker only scopes the member list; it does not grant access (a link alone gives
// self-service scope for that one member).
const memberHouseholdId = ref('')
const memberHhRef = computed(() => memberHouseholdId.value)
const { members: membersInHousehold } = useHouseholdMembers(memberHhRef)
const memberPickerOptions = computed(() => membersInHousehold.value.map(m => ({ label: m.name, value: m.id })))
const linkedMemberId = ref<string | undefined>(undefined)

// Changing the household invalidates any member chosen from the previous one.
watch(memberHouseholdId, () => {
  linkedMemberId.value = undefined
})

// ── Household Access (optional, multiple) — mirrors the Household Access card ──
const addingAccess = ref(false)
const unassignedHouseholdOptions = computed(() =>
  households.value
    .filter(h => !form.households.some(g => g.householdId === h.id))
    .map(h => ({ label: h.name, value: h.id })),
)

function startAddAccess() {
  addingAccess.value = true
}

function cancelAddAccess() {
  addingAccess.value = false
}

function onNewHouseholdSelected(householdId: string) {
  if (!form.households.some(g => g.householdId === householdId))
    form.households.push({ householdId, role: 'Member' })
  addingAccess.value = false
}

function removeHouseholdGrant(householdId: string) {
  form.households = form.households.filter(g => g.householdId !== householdId)
}

watch(open, (isOpen) => {
  if (isOpen) {
    form.email = ''
    form.role = 'Member'
    form.households = []
    memberHouseholdId.value = ''
    linkedMemberId.value = undefined
    addingAccess.value = false
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
  const ok = await invite(email, form.role, [...form.households], linkedMemberId.value ?? null)
  if (ok) open.value = false
}
</script>

<template>
  <UModal v-model:open="open" :title="t('tenantUser.invite.title')">
    <template #body>
      <div class="space-y-6">
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

        <!-- Linked Member -->
        <div class="space-y-2">
          <p class="font-semibold text-sm">{{ t('tenantUser.linkedMember') }}</p>
          <div class="flex items-center gap-3 flex-wrap">
            <USelectMenu
              v-model="memberHouseholdId"
              :items="householdOptions"
              value-key="value"
              :placeholder="t('household.title')"
              class="w-44"
            />
            <USelectMenu
              v-if="memberHouseholdId && memberPickerOptions.length"
              v-model="linkedMemberId"
              :items="memberPickerOptions"
              value-key="value"
              :placeholder="t('member.title')"
              class="w-44"
            />
          </div>
          <p v-if="!linkedMemberId" class="text-sm text-muted">{{ t('tenantUser.noLinkedMember') }}</p>
        </div>

        <!-- Household Access -->
        <div class="space-y-2">
          <div class="flex items-center justify-between">
            <p class="font-semibold text-sm">{{ t('tenantUser.householdAccess') }}</p>
            <UButton
              v-if="!addingAccess && unassignedHouseholdOptions.length"
              size="sm"
              variant="ghost"
              icon="i-heroicons-plus"
              @click="startAddAccess"
            >
              {{ t('tenantUser.addHouseholdAccess') }}
            </UButton>
          </div>

          <!-- New-access row -->
          <div v-if="addingAccess" class="flex items-center gap-3 pb-3 border-b border-(--ui-border)">
            <USelectMenu
              :items="unassignedHouseholdOptions"
              value-key="value"
              :placeholder="t('household.title')"
              class="w-44"
              @update:model-value="(v) => onNewHouseholdSelected(v as unknown as string)"
            />
            <UButton variant="ghost" color="neutral" size="sm" @click="cancelAddAccess">
              {{ t('common.cancel') }}
            </UButton>
          </div>

          <!-- Existing access list -->
          <div
            v-for="grant in form.households"
            :key="grant.householdId"
            class="flex items-center gap-3"
          >
            <span class="flex-1 text-sm truncate">{{ householdName(grant.householdId) }}</span>
            <USelectMenu
              v-model="grant.role"
              :items="hhRoleOptions"
              value-key="value"
              class="w-36"
            />
            <UButton
              variant="ghost"
              color="error"
              size="sm"
              icon="i-heroicons-x-mark"
              :aria-label="t('tenantUser.removeAccess')"
              @click="removeHouseholdGrant(grant.householdId)"
            />
          </div>

          <p v-if="!form.households.length && !addingAccess" class="text-sm text-muted">
            {{ t('tenantUser.noHouseholdAccess') }}
          </p>
        </div>
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

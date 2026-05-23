<script setup lang="ts">
import { useTenantUserList, useTenantUserActions } from '~/composables/useTenantUsers'
import { useUserHouseholdAccess, useHouseholdUserActions } from '~/composables/useHouseholdUsers'
import { useHouseholds } from '~/composables/useHouseholds'
import { useHouseholdMembers, useAllMembers } from '~/composables/useHouseholdMembers'
import { useCurrentMemberStore } from '~/stores/member'
import type { HouseholdRole, TenantRole } from '~/repositories/types'

definePageMeta({
  layout: 'default',
})

const config = useRuntimeConfig()
const { t } = useI18n()
const route = useRoute()
const userId = computed(() => route.params.userId as string)

// Tenant user data
const { tenantUsers, pending: usersPending, refresh: refreshUsers } = useTenantUserList()
const targetUser = computed(() => tenantUsers.value.find(u => u.userId === userId.value))
const { updateRole, setLinkedMember, updating: userUpdating } = useTenantUserActions(refreshUsers)

// Resolve linked member details from the full member map
const { memberMap } = useAllMembers()
const linkedMember = computed(() =>
  targetUser.value?.linkedMemberId ? (memberMap.value.get(targetUser.value.linkedMemberId) ?? null) : null,
)

// Linked member section — household and member pickers
const memberHouseholdId = ref('')
const memberHhRef = computed(() => memberHouseholdId.value)
const { members: membersInHousehold } = useHouseholdMembers(memberHhRef)

// Auto-populate household picker from the linked member's household
watch([memberMap, () => targetUser.value?.linkedMemberId], ([, linkedMemberId]) => {
  if (linkedMemberId && memberMap.value.size > 0) {
    const m = memberMap.value.get(linkedMemberId)
    if (m) memberHouseholdId.value = m.householdId
  }
  else if (!linkedMemberId) {
    memberHouseholdId.value = ''
  }
}, { immediate: true })

const selectedLinkedMemberId = computed(() => {
  if (!targetUser.value?.linkedMemberId) return undefined
  return memberPickerOptions.value.some(o => o.value === targetUser.value!.linkedMemberId)
    ? targetUser.value.linkedMemberId
    : undefined
})

const currentMemberStore = useCurrentMemberStore()

async function handleSetLinkedMember(memberId: string | null) {
  await setLinkedMember(userId.value, memberId)
  if (config.public.demoMode) {
    if (memberId && memberHouseholdId.value) {
      currentMemberStore.setLinkedMember(memberId, memberHouseholdId.value)
    }
    else {
      currentMemberStore.clear()
    }
  }
}

// Household access section
const { access: householdAccess, refresh: refreshAccess } = useUserHouseholdAccess(userId)
const { upsertHouseholdUser, deleteHouseholdUser, updating: hhUpdating } = useHouseholdUserActions(refreshAccess)

const { households } = useHouseholds()

const assignedHouseholdIds = computed(() => new Set(householdAccess.value.map(a => a.householdId)))

const unassignedHouseholdOptions = computed(() =>
  households.value
    .filter(h => !assignedHouseholdIds.value.has(h.id))
    .map(h => ({ label: h.name, value: h.id })),
)

const addingAccess = ref(false)

function startAddAccess() {
  addingAccess.value = true
}

function cancelAddAccess() {
  addingAccess.value = false
}

async function onNewHouseholdSelected(householdId: string) {
  await upsertHouseholdUser(householdId, userId.value, 'Member')
  addingAccess.value = false
}

function householdName(householdId: string): string {
  return households.value.find(h => h.id === householdId)?.name ?? householdId
}

const roleOptions: { label: string, value: TenantRole }[] = [
  { label: t('tenantUser.roles.Owner'), value: 'Owner' },
  { label: t('tenantUser.roles.Manager'), value: 'Manager' },
  { label: t('tenantUser.roles.Coordinator'), value: 'Coordinator' },
  { label: t('tenantUser.roles.Member'), value: 'Member' },
  { label: t('tenantUser.roles.Guest'), value: 'Guest' },
]

const hhRoleOptions: { label: string, value: HouseholdRole }[] = [
  { label: t('tenantUser.householdRoles.Manager'), value: 'Manager' },
  { label: t('tenantUser.householdRoles.Member'), value: 'Member' },
]

const householdOptions = computed(() =>
  households.value.map(h => ({ label: h.name, value: h.id })),
)

const memberPickerOptions = computed(() =>
  membersInHousehold.value.map(m => ({ label: m.name, value: m.id })),
)
</script>

<template>
  <div>
    <GsBreadcrumb
      :items="[
        { label: t('settings.title'), to: '/app/settings' },
        { label: t('settings.users'), to: '/app/settings/users' },
        { label: targetUser?.externalId ?? '…' },
      ]"
    />
    <GsPageHeader :title="targetUser?.externalId ?? t('common.loading')" />

    <div v-if="usersPending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <div v-else-if="!targetUser" class="py-16 text-center">
      <p class="text-muted">{{ t('common.error') }}</p>
    </div>

    <div v-else class="flex flex-col gap-6 max-w-2xl">
      <!-- Tenant Role -->
      <UCard>
        <template #header>
          <p class="font-semibold">{{ t('tenantUser.role') }}</p>
        </template>
        <USelectMenu
          :model-value="targetUser.role"
          :items="roleOptions"
          value-key="value"
          :disabled="userUpdating.includes(userId)"
          class="w-44"
          @update:model-value="(v) => updateRole(userId, v as unknown as TenantRole)"
        />
      </UCard>

      <!-- Linked Member -->
      <UCard>
        <template #header>
          <p class="font-semibold">{{ t('tenantUser.linkedMember') }}</p>
        </template>
        <div class="flex flex-col gap-2">
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
              :model-value="selectedLinkedMemberId"
              :items="memberPickerOptions"
              value-key="value"
              :placeholder="t('member.title')"
              class="w-44"
              :disabled="userUpdating.includes(userId)"
              @update:model-value="(v) => handleSetLinkedMember(v as unknown as string)"
            />
            <UButton
              v-if="targetUser.linkedMemberId"
              variant="ghost"
              color="error"
              :disabled="userUpdating.includes(userId)"
              @click="handleSetLinkedMember(null)"
            >
              {{ t('tenantUser.removeAccess') }}
            </UButton>
            <UTooltip :text="t('tenantUser.linkedMemberSessionNote')">
              <UIcon name="i-heroicons-information-circle" class="text-muted cursor-help" />
            </UTooltip>
          </div>
          <p v-if="targetUser.linkedMemberId" class="text-xs text-muted font-mono">
            {{ targetUser.linkedMemberId }}
          </p>
          <p v-else-if="!linkedMember" class="text-sm text-muted">
            {{ t('tenantUser.noLinkedMember') }}
          </p>
        </div>
      </UCard>

      <!-- Household Access -->
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <p class="font-semibold">{{ t('tenantUser.householdAccess') }}</p>
            <UButton
              v-if="!addingAccess && unassignedHouseholdOptions.length"
              size="sm"
              variant="ghost"
              icon="i-heroicons-plus"
              :disabled="hhUpdating.includes(userId)"
              @click="startAddAccess"
            >
              {{ t('tenantUser.addHouseholdAccess') }}
            </UButton>
          </div>
        </template>
        <div class="flex flex-col gap-2">
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
            v-for="entry in householdAccess"
            :key="entry.householdId"
            class="flex items-center gap-3"
          >
            <span class="flex-1 text-sm truncate">{{ householdName(entry.householdId) }}</span>
            <USelectMenu
              :model-value="entry.role"
              :items="hhRoleOptions"
              value-key="value"
              :disabled="hhUpdating.includes(userId)"
              class="w-36"
              @update:model-value="(v) => upsertHouseholdUser(entry.householdId, userId, v as unknown as HouseholdRole)"
            />
            <UButton
              variant="ghost"
              color="error"
              size="sm"
              :disabled="hhUpdating.includes(userId)"
              @click="deleteHouseholdUser(entry.householdId, userId)"
            >
              {{ t('tenantUser.removeAccess') }}
            </UButton>
          </div>

          <p v-if="!householdAccess.length && !addingAccess" class="text-sm text-muted">
            {{ t('tenantUser.noHouseholdAccess') }}
          </p>
        </div>
      </UCard>
    </div>
  </div>
</template>

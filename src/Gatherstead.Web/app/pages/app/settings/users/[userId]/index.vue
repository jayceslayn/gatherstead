<script setup lang="ts">
import { useTenantUserList, useTenantUserActions } from '~/composables/useTenantUsers'
import { useHouseholdUsers, useHouseholdUserActions } from '~/composables/useHouseholdUsers'
import { useHouseholds } from '~/composables/useHouseholds'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import type { HouseholdRole, TenantRole } from '~/repositories/types'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const userId = computed(() => route.params.userId as string)

const { tenantUsers, pending: usersPending } = useTenantUserList()
const targetUser = computed(() => tenantUsers.value.find(u => u.userId === userId.value))

const { updateRole, setLinkedMember, updating: userUpdating } = useTenantUserActions()

const { households } = useHouseholds()

// Household user management
const selectedHouseholdId = ref<string>('')
const hhIdRef = computed(() => selectedHouseholdId.value)
const { householdUsers, refresh: refreshHouseholdUsers } = useHouseholdUsers(hhIdRef)
const { upsertHouseholdUser, deleteHouseholdUser, updating: hhUpdating } = useHouseholdUserActions(hhIdRef, refreshHouseholdUsers)

const userInSelectedHousehold = computed(() =>
  householdUsers.value.find(hu => hu.userId === userId.value),
)

// Member picker for linked member
const memberHouseholdId = ref<string>('')
const memberHhRef = computed(() => memberHouseholdId.value)
const { members: membersInHousehold } = useHouseholdMembers(memberHhRef)

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
          :options="roleOptions"
          value-attribute="value"
          option-attribute="label"
          :disabled="userUpdating.includes(userId)"
          class="w-44"
          @update:model-value="(v) => updateRole(userId, (v as { value: TenantRole }).value)"
        />
      </UCard>

      <!-- Linked Member -->
      <UCard>
        <template #header>
          <p class="font-semibold">{{ t('tenantUser.linkedMember') }}</p>
        </template>
        <div class="flex flex-col gap-3">
          <p class="text-sm">
            <span class="text-muted">{{ t('tenantUser.linkedMemberLabel') }}</span>
            {{ targetUser.linkedMemberId ?? t('tenantUser.noLinkedMember') }}
          </p>

          <div class="flex items-center gap-3 flex-wrap">
            <USelectMenu
              v-model="memberHouseholdId"
              :options="householdOptions"
              value-attribute="value"
              option-attribute="label"
              :placeholder="t('household.title')"
              class="w-44"
            />
            <USelectMenu
              v-if="memberHouseholdId && memberPickerOptions.length"
              :options="memberPickerOptions"
              value-attribute="value"
              option-attribute="label"
              :placeholder="t('member.title')"
              class="w-44"
              :disabled="userUpdating.includes(userId)"
              @update:model-value="(v) => setLinkedMember(userId, (v as { value: string }).value)"
            />
            <UButton
              v-if="targetUser.linkedMemberId"
              variant="ghost"
              color="error"
              :disabled="userUpdating.includes(userId)"
              @click="setLinkedMember(userId, null)"
            >
              {{ t('tenantUser.removeAccess') }}
            </UButton>
          </div>
        </div>
      </UCard>

      <!-- Household Access -->
      <UCard>
        <template #header>
          <p class="font-semibold">{{ t('tenantUser.householdAccess') }}</p>
        </template>
        <div class="flex flex-col gap-4">
          <USelectMenu
            v-model="selectedHouseholdId"
            :options="householdOptions"
            value-attribute="value"
            option-attribute="label"
            :placeholder="t('household.title')"
            class="w-44"
          />

          <div v-if="selectedHouseholdId">
            <div v-if="userInSelectedHousehold" class="flex items-center gap-3">
              <USelectMenu
                :model-value="userInSelectedHousehold.role"
                :options="hhRoleOptions"
                value-attribute="value"
                option-attribute="label"
                :disabled="hhUpdating.includes(userId)"
                class="w-36"
                @update:model-value="(v) => upsertHouseholdUser(userId, (v as { value: HouseholdRole }).value)"
              />
              <UButton
                variant="ghost"
                color="error"
                :disabled="hhUpdating.includes(userId)"
                @click="deleteHouseholdUser(userId)"
              >
                {{ t('tenantUser.removeAccess') }}
              </UButton>
            </div>
            <div v-else class="flex items-center gap-3">
              <p class="text-sm text-muted flex-1">{{ t('tenantUser.noHouseholdAccess') }}</p>
              <UButton
                :disabled="hhUpdating.includes(userId)"
                @click="upsertHouseholdUser(userId, 'Member')"
              >
                {{ t('tenantUser.addHouseholdAccess') }}
              </UButton>
            </div>
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>

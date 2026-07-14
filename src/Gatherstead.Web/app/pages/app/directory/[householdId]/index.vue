<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import { useHousehold, useHouseholdActions } from '~/composables/useHouseholds'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useAgeBands } from '~/composables/useAgeBands'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const { isManagerOrAbove } = useTenantRole()

const householdId = computed(() => route.params.householdId as string)
const { household, pending: householdPending, refresh: refreshHousehold } = useHousehold(householdId)
const { members, pending: membersPending } = useHouseholdMembers(householdId)
const { displayName: ageBandDisplayName } = useAgeBands()

const pending = computed(() => householdPending.value || membersPending.value)

const showDeleteConfirm = ref(false)
const { deleteHousehold } = useHouseholdActions(refreshHousehold)

async function confirmDelete() {
  showDeleteConfirm.value = false
  await deleteHousehold(householdId.value)
  await router.push('/app/directory')
}

// Rename / edit — handled by the shared GsHouseholdModal in edit mode.
const showEdit = ref(false)

function onModalDelete() {
  showEdit.value = false
  showDeleteConfirm.value = true
}
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="household">
      <GsBreadcrumb
        :items="[
          { label: t('household.title'), to: '/app/directory' },
          { label: household.name },
        ]"
      />

      <GsPageHeader :title="household.name">
        <GsRoleGate min-role="Manager">
          <div class="flex items-center gap-2">
            <UButton
              variant="outline"
              size="sm"
              icon="i-heroicons-pencil"
              @click="() => { showEdit = true }"
            >
              {{ t('common.edit') }}
            </UButton>
            <UButton
              :to="`/app/directory/${household.id}/create-member`"
              size="sm"
              icon="i-heroicons-plus"
            >
              {{ t('member.createMember') }}
            </UButton>
          </div>
        </GsRoleGate>
      </GsPageHeader>

      <GsNotesSection :notes="household.notes" class="mb-6 max-w-lg" />

      <GsAttributeSection :attributes="household.attributes" class="mb-6 max-w-lg" />

      <GsEmptyState
        v-if="!members.length"
        icon="i-heroicons-user-group"
        :title="t('member.noMembers')"
        :description="isManagerOrAbove ? t('member.noMembersHintManager') : t('member.noMembersHintMember')"
      >
        <UButton v-if="isManagerOrAbove" :to="`/app/directory/${household.id}/create-member`" icon="i-heroicons-plus">
          {{ t('member.createMember') }}
        </UButton>
      </GsEmptyState>

      <div v-else class="flex flex-col gap-3">
        <NuxtLink
          v-for="member in members"
          :key="member.id"
          :to="`/app/directory/${household.id}/${member.id}`"
        >
          <UCard class="hover:ring-1 hover:ring-primary transition-all cursor-pointer">
            <div class="flex items-center gap-3">
              <GsMemberAvatar :name="member.name" size="sm" />
              <div class="min-w-0 flex-1">
                <p class="font-semibold">{{ member.name }}</p>
                <div class="flex items-center gap-1.5 mt-0.5">
                  <span v-if="member.isAdult != null" class="text-sm text-muted">
                    {{ member.isAdult ? t('member.adult') : t('member.child') }}
                  </span>
                  <!-- eslint-disable-next-line @intlify/vue-i18n/no-raw-text -->
                  <span v-if="member.ageBand" class="text-sm text-muted">· {{ ageBandDisplayName(member.ageBand) }}</span>
                </div>
              </div>
              <GsDietaryTags :slugs="member.dietaryTags" class="hidden sm:flex" />
              <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
            </div>
          </UCard>
        </NuxtLink>
      </div>

    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />

    <GsConfirmModal
      v-model:open="showDeleteConfirm"
      :title="t('household.deleteTitle')"
      :description="t('household.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDelete"
    />

    <GsHouseholdModal
      v-model:open="showEdit"
      :household="household"
      :refresh="refreshHousehold"
      @delete="onModalDelete"
    />
  </div>
</template>

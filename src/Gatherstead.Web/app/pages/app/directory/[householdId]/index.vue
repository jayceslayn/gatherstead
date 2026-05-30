<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'
import { useHousehold, useHouseholdActions } from '~/composables/useHouseholds'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'

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

const pending = computed(() => householdPending.value || membersPending.value)

const showDeleteConfirm = ref(false)
const { updating, deleteHousehold } = useHouseholdActions(async () => {
  await router.push('/app/directory')
})
const deleting = computed(() => updating.value.includes(householdId.value))

async function confirmDelete() {
  showDeleteConfirm.value = false
  await deleteHousehold(householdId.value)
}

// Rename / edit
const { updating: editUpdating, updateHousehold } = useHouseholdActions(refreshHousehold)
const showEdit = ref(false)
const editName = ref('')
const editError = ref('')
const savingEdit = computed(() => editUpdating.value.includes(householdId.value))

function openEdit() {
  editName.value = household.value?.name ?? ''
  editError.value = ''
  showEdit.value = true
}

async function submitEdit() {
  editError.value = ''
  const trimmed = editName.value.trim()
  if (!trimmed) {
    editError.value = t('validation.required', { field: t('household.name') })
    return
  }
  await updateHousehold(householdId.value, trimmed)
  showEdit.value = false
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
              @click="openEdit"
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
                  <span class="text-sm text-muted">
                    {{ member.isAdult ? t('member.adult') : t('member.child') }}
                  </span>
                  <!-- eslint-disable-next-line @intlify/vue-i18n/no-raw-text -->
                  <span v-if="member.ageBand" class="text-sm text-muted">· {{ member.ageBand }}</span>
                </div>
              </div>
              <GsDietaryTags :dietary-tags="member.dietaryTags" class="hidden sm:flex" />
              <UIcon name="i-heroicons-chevron-right" class="size-5 text-muted shrink-0" />
            </div>
          </UCard>
        </NuxtLink>
      </div>

      <GsRoleGate min-role="Manager">
        <div class="mt-12 pt-6 border-t border-default">
          <UButton
            color="error"
            variant="ghost"
            icon="i-heroicons-trash"
            :loading="deleting"
            @click="showDeleteConfirm = true"
          >
            {{ t('household.deleteTitle') }}
          </UButton>
        </div>
      </GsRoleGate>
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

    <UModal v-model:open="showEdit">
      <template #content>
        <div class="p-6">
          <h3 class="text-lg font-semibold mb-4">{{ t('household.editTitle') }}</h3>
          <UFormField :label="t('household.name')" :error="editError || undefined">
            <UInput
              v-model="editName"
              :placeholder="t('household.name')"
              @keydown.enter="submitEdit"
            />
          </UFormField>
          <div class="flex justify-end gap-3 mt-6">
            <UButton variant="ghost" :disabled="savingEdit" @click="showEdit = false">
              {{ t('common.cancel') }}
            </UButton>
            <UButton :loading="savingEdit" @click="submitEdit">
              {{ t('common.save') }}
            </UButton>
          </div>
        </div>
      </template>
    </UModal>
  </div>
</template>

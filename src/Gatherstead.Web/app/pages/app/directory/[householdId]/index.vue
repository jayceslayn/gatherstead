<script setup lang="ts">
import { useTenantRole } from '~/composables/useTenantRole'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const { isManagerOrAbove } = useTenantRole()

const householdId = computed(() => route.params.householdId as string)
const { household, pending: householdPending } = useHousehold(householdId)
const { members, pending: membersPending } = useHouseholdMembers(householdId)

const pending = computed(() => householdPending.value || membersPending.value)
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="household">
      <GsBreadcrumb :items="[
        { label: t('household.title'), to: '/app/directory' },
        { label: household.name },
      ]" />

      <GsPageHeader :title="household.name">
        <GsRoleGate min-role="Manager">
          <UButton
            :to="`/app/directory/${household.id}/add-member`"
            size="sm"
            icon="i-heroicons-plus"
          >
            {{ t('member.addMember') }}
          </UButton>
        </GsRoleGate>
      </GsPageHeader>

      <GsEmptyState
        v-if="!members.length"
        icon="i-heroicons-user-group"
        :title="t('member.noMembers')"
        :description="isManagerOrAbove ? t('member.noMembersHintManager') : t('member.noMembersHintMember')"
      >
        <UButton v-if="isManagerOrAbove" :to="`/app/directory/${household.id}/add-member`" icon="i-heroicons-plus">
          {{ t('member.addMember') }}
        </UButton>
      </GsEmptyState>

      <div v-else class="space-y-2">
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
                  <span v-if="member.ageBand" class="text-sm text-muted">· {{ member.ageBand }}</span>
                </div>
              </div>
              <GsDietaryTags :dietary-tags="member.dietaryTags" class="hidden sm:flex" />
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
  </div>
</template>

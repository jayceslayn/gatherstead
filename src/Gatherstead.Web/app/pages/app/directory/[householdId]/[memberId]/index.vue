<script setup lang="ts">
import { useCurrentMemberStore } from '~/stores/member'
import { useTenantRole } from '~/composables/useTenantRole'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const memberStore = useCurrentMemberStore()

const householdId = computed(() => route.params.householdId as string)
const memberId = computed(() => route.params.memberId as string)

const { household } = useHousehold(householdId)
const { member, pending: memberPending } = useMember(householdId, memberId)
const { dietaryProfile, pending: profilePending } = useDietaryProfile(householdId, memberId)

const pending = computed(() => memberPending.value || profilePending.value)

const isSelf = computed(() => memberStore.linkedMemberId === memberId.value)
const { isManagerOrAbove } = useTenantRole()
const canEdit = computed(() => isSelf.value || isManagerOrAbove.value)

function formatDate(date: string | null) {
  if (!date) return null
  return new Intl.DateTimeFormat(undefined, { year: 'numeric', month: 'long', day: 'numeric' }).format(
    new Date(date + 'T00:00:00'),
  )
}
</script>

<template>
  <div>
    <div v-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="member">
      <GsBreadcrumb :items="[
        { label: t('household.title'), to: '/app/directory' },
        { label: household?.name ?? '…', to: `/app/directory/${householdId}` },
        { label: member.name },
      ]" />

      <GsPageHeader :title="member.name">
        <UButton
          v-if="canEdit"
          :to="`/app/directory/${householdId}/${memberId}/edit`"
          variant="outline"
          size="sm"
          icon="i-heroicons-pencil"
        >
          {{ t('common.edit') }}
        </UButton>
      </GsPageHeader>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
        <!-- Identity -->
        <UCard>
          <template #header>
            <p class="font-semibold">{{ t('member.identity') }}</p>
          </template>
          <dl class="space-y-3 text-sm">
            <div class="flex justify-between gap-4">
              <dt class="text-muted">{{ t('member.isAdult') }}</dt>
              <dd>{{ member.isAdult ? t('member.adult') : t('member.child') }}</dd>
            </div>
            <div v-if="member.ageBand" class="flex justify-between gap-4">
              <dt class="text-muted">{{ t('member.ageBand') }}</dt>
              <dd>{{ member.ageBand }}</dd>
            </div>
            <div v-if="member.birthDate" class="flex justify-between gap-4">
              <dt class="text-muted">{{ t('member.birthDate') }}</dt>
              <dd>{{ formatDate(member.birthDate) }}</dd>
            </div>
          </dl>
        </UCard>

        <!-- Dietary Profile -->
        <UCard>
          <template #header>
            <p class="font-semibold">{{ t('member.dietaryProfile') }}</p>
          </template>

          <div v-if="!dietaryProfile && !member.dietaryTags.length && !member.dietaryNotes" class="text-sm text-muted">
            {{ t('member.noDietaryProfile') }}
          </div>

          <dl v-else class="space-y-3 text-sm">
            <div v-if="dietaryProfile?.preferredDiet" class="flex justify-between gap-4">
              <dt class="text-muted">{{ t('member.preferredDiet') }}</dt>
              <dd>{{ dietaryProfile.preferredDiet }}</dd>
            </div>

            <div v-if="dietaryProfile?.allergies?.length">
              <dt class="text-muted mb-1.5">{{ t('member.allergies') }}</dt>
              <dd>
                <GsDietaryTags :allergies="dietaryProfile.allergies" />
              </dd>
            </div>

            <div v-if="dietaryProfile?.restrictions?.length">
              <dt class="text-muted mb-1.5">{{ t('member.restrictions') }}</dt>
              <dd>
                <GsDietaryTags :restrictions="dietaryProfile.restrictions" />
              </dd>
            </div>

            <div v-if="member.dietaryTags.length">
              <dt class="text-muted mb-1.5">{{ t('member.dietaryTags') }}</dt>
              <dd>
                <GsDietaryTags :dietary-tags="member.dietaryTags" />
              </dd>
            </div>

            <div v-if="member.dietaryNotes" class="flex flex-col gap-1">
              <dt class="text-muted">{{ t('member.dietaryNotes') }}</dt>
              <dd class="whitespace-pre-wrap">{{ member.dietaryNotes }}</dd>
            </div>

            <div v-if="dietaryProfile?.notes" class="flex flex-col gap-1">
              <dt class="text-muted">{{ t('common.notes') }}</dt>
              <dd class="whitespace-pre-wrap">{{ dietaryProfile.notes }}</dd>
            </div>
          </dl>
        </UCard>
      </div>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>

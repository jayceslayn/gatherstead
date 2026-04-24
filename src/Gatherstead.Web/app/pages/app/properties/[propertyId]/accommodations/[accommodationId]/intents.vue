<script setup lang="ts">
import { useProperty } from '~/composables/useProperties'
import { useAccommodations, useAccommodationIntents, useAccommodationIntentActions } from '~/composables/useAccommodations'
import { useAllMembers } from '~/composables/useHouseholdMembers'
import { useCurrentMemberStore } from '~/stores/member'
import { useTenantRole } from '~/composables/useTenantRole'
import type { AccommodationIntent, AccommodationIntentStatus, AccommodationIntentDecision } from '~/repositories/types'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const route = useRoute()
const memberStore = useCurrentMemberStore()
const { isManagerOrAbove } = useTenantRole()

const propertyId = computed(() => route.params.propertyId as string)
const accommodationId = computed(() => route.params.accommodationId as string)

const { property } = useProperty(propertyId)

const { accommodations } = useAccommodations(propertyId)
const accommodation = computed(() =>
  accommodations.value.find(a => a.id === accommodationId.value) ?? null,
)

const memberId = computed<string | null>(() =>
  isManagerOrAbove.value ? null : (memberStore.linkedMemberId ?? null),
)

const { intents, pending, refresh } = useAccommodationIntents(propertyId, accommodationId, memberId)
const { updating, requestIntent, promoteIntent } = useAccommodationIntentActions(propertyId, accommodationId, refresh)

const { memberMap, pending: membersPending } = useAllMembers()

function memberName(id: string): string {
  return memberMap.value.get(id)?.name ?? id.slice(-8)
}

const nightGroups = computed(() => {
  const map = new Map<string, AccommodationIntent[]>()
  for (const intent of intents.value) {
    const group = map.get(intent.night) ?? []
    group.push(intent)
    map.set(intent.night, group)
  }
  return [...map.entries()].sort(([a], [b]) => a.localeCompare(b))
})

function formatNight(dateStr: string): string {
  return new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'long', day: 'numeric' })
    .format(new Date(dateStr + 'T00:00:00'))
}

// Status promotion flow for managers
const STATUS_PROGRESSION: Partial<Record<AccommodationIntentStatus, AccommodationIntentStatus>> = {
  Intent: 'Hold',
  Hold: 'Confirmed',
}
const DECISION_FOR_STATUS: Partial<Record<AccommodationIntentStatus, AccommodationIntentDecision>> = {
  Hold: 'Pending',
  Confirmed: 'Approved',
}

function nextStatus(current: AccommodationIntentStatus): AccommodationIntentStatus | null {
  return STATUS_PROGRESSION[current] ?? null
}

async function handlePromote(intent: AccommodationIntent) {
  const next = nextStatus(intent.status)
  if (!next) return
  const decision = DECISION_FOR_STATUS[next] ?? 'Pending'
  await promoteIntent(intent.id, next, decision, intent.notes, intent.partySize)
}

async function handleDecline(intent: AccommodationIntent) {
  await promoteIntent(intent.id, intent.status, 'Declined', intent.notes, intent.partySize)
}

// Request modal for members
const showRequestModal = ref(false)
const requestNight = ref('')
const requestStatus = ref<AccommodationIntentStatus>('Intent')
const requestNotes = ref('')
const requestPartySize = ref<number | null>(null)
const requestLoading = ref(false)

const statusOptions: { label: string; value: AccommodationIntentStatus }[] = [
  { label: t('status.intent'), value: 'Intent' },
  { label: t('status.hold'), value: 'Hold' },
]

function openRequestModal(night?: string) {
  requestNight.value = night ?? ''
  requestStatus.value = 'Intent'
  requestNotes.value = ''
  requestPartySize.value = null
  showRequestModal.value = true
}

async function submitRequest() {
  if (!memberStore.linkedMemberId || !memberStore.linkedHouseholdId || !requestNight.value) return
  requestLoading.value = true
  await requestIntent(
    memberStore.linkedHouseholdId,
    memberStore.linkedMemberId,
    requestNight.value,
    requestStatus.value,
    requestNotes.value || null,
    requestPartySize.value,
  )
  requestLoading.value = false
  showRequestModal.value = false
}
</script>

<template>
  <div>
    <GsBreadcrumb
      :items="[
        { label: t('property.title'), to: '/app/properties' },
        { label: property?.name ?? '…', to: `/app/properties/${propertyId}` },
        { label: accommodation?.name ?? '…' },
      ]"
    />

    <GsPageHeader
      :title="accommodation?.name ?? t('common.loading')"
      :subtitle="property?.name"
    />

    <div v-if="accommodation" class="flex flex-wrap gap-3 mb-6">
      <UBadge color="neutral" variant="soft">
        {{ t(`accommodation.types.${accommodation.type.charAt(0).toLowerCase() + accommodation.type.slice(1)}`) }}
      </UBadge>
      <UBadge v-if="accommodation.capacityAdults" color="neutral" variant="soft">
        {{ t('accommodation.adults', { n: accommodation.capacityAdults }, accommodation.capacityAdults) }}
      </UBadge>
      <UBadge v-if="accommodation.capacityChildren" color="neutral" variant="soft">
        {{ t('accommodation.children', { n: accommodation.capacityChildren }, accommodation.capacityChildren) }}
      </UBadge>
    </div>

    <div class="flex items-center justify-between mb-4">
      <h2 class="text-base font-semibold">{{ t('accommodation.intents') }}</h2>
      <UButton
        v-if="memberStore.linkedMemberId && !isManagerOrAbove"
        size="sm"
        icon="i-heroicons-plus"
        @click="openRequestModal()"
      >
        {{ t('accommodation.requestStay') }}
      </UButton>
    </div>

    <div v-if="pending || membersPending" class="py-8 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!intents.length"
      icon="i-heroicons-hand-raised"
      :title="t('accommodation.noIntents')"
    >
      <UButton
        v-if="memberStore.linkedMemberId && !isManagerOrAbove"
        icon="i-heroicons-plus"
        @click="openRequestModal()"
      >
        {{ t('accommodation.requestStay') }}
      </UButton>
    </GsEmptyState>

    <div v-else class="space-y-6">
      <div
        v-for="[night, group] in nightGroups"
        :key="night"
      >
        <div class="flex items-center justify-between mb-2">
          <h3 class="text-sm font-semibold text-muted">
            {{ formatNight(night) }}
          </h3>
          <UButton
            v-if="memberStore.linkedMemberId && !isManagerOrAbove"
            variant="ghost"
            size="xs"
            icon="i-heroicons-plus"
            @click="openRequestModal(night)"
          >
            {{ t('accommodation.requestStay') }}
          </UButton>
        </div>

        <div class="space-y-2">
          <UCard
            v-for="intent in group"
            :key="intent.id"
          >
            <div class="flex items-center gap-3 flex-wrap">
              <GsMemberAvatar :name="memberName(intent.householdMemberId)" size="sm" />
              <div class="flex-1 min-w-0">
                <p class="font-medium text-sm truncate">{{ memberName(intent.householdMemberId) }}</p>
                <div class="flex flex-wrap gap-2 mt-0.5">
                  <GsStatusBadge :status="intent.status" size="xs" />
                  <UBadge
                    v-if="intent.decision !== 'Pending'"
                    :color="intent.decision === 'Approved' ? 'success' : 'error'"
                    variant="soft"
                    size="xs"
                  >
                    {{ t(`accommodation.decision.${intent.decision.toLowerCase()}`) }}
                  </UBadge>
                  <span v-if="intent.partySize" class="text-xs text-muted self-center">
                    {{ t('accommodation.partySizeValue', { n: intent.partySize }) }}
                  </span>
                </div>
                <p v-if="intent.notes" class="text-xs text-muted mt-1">{{ intent.notes }}</p>
              </div>

              <!-- Manager promotion controls -->
              <GsRoleGate min-role="Manager">
                <div class="flex items-center gap-1 shrink-0">
                  <UButton
                    v-if="nextStatus(intent.status)"
                    size="xs"
                    color="success"
                    variant="soft"
                    :loading="updating.includes(intent.id)"
                    @click="handlePromote(intent)"
                  >
                    {{ t('accommodation.promote') }}
                  </UButton>
                  <UButton
                    v-if="intent.decision !== 'Declined'"
                    size="xs"
                    color="error"
                    variant="soft"
                    :loading="updating.includes(intent.id)"
                    @click="handleDecline(intent)"
                  >
                    {{ t('accommodation.decline') }}
                  </UButton>
                </div>
              </GsRoleGate>
            </div>
          </UCard>
        </div>
      </div>
    </div>

    <!-- Request stay modal (members) -->
    <UModal v-model:open="showRequestModal" :title="t('accommodation.requestStay')">
      <template #body>
        <div class="space-y-4">
          <UFormField :label="t('accommodation.nightOf', { date: '' })">
            <UInput v-model="requestNight" type="date" />
          </UFormField>
          <UFormField :label="t('accommodation.status')">
            <USelect v-model="requestStatus" :items="statusOptions" />
          </UFormField>
          <UFormField :label="t('accommodation.partySize')">
            <UInput v-model.number="requestPartySize" type="number" min="1" />
          </UFormField>
          <UFormField :label="t('common.notes')">
            <UTextarea v-model="requestNotes" :rows="2" />
          </UFormField>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton variant="outline" @click="showRequestModal = false">{{ t('common.cancel') }}</UButton>
          <UButton :loading="requestLoading" @click="submitRequest">{{ t('accommodation.requestStay') }}</UButton>
        </div>
      </template>
    </UModal>
  </div>
</template>

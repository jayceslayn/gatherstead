<script setup lang="ts">
import { useProperty } from '~/composables/useProperties'
import { useAccommodations, useAccommodationIntents, useAccommodationIntentActions } from '~/composables/useAccommodations'
import { useAllMembers } from '~/composables/useHouseholdMembers'
import { useCurrentMemberStore } from '~/stores/member'
import { useTenantRole } from '~/composables/useTenantRole'
import type { AccommodationIntent, AccommodationIntentStatus, HouseholdMember } from '~/repositories/types'
import { formatArea } from '~/utils/units'
import { formatBedSummary } from '~/utils/beds'

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
const { formatDateRange } = useFormatDate()

function memberName(id: string): string {
  return memberMap.value.get(id)?.name ?? id.slice(-8)
}

// One card per stay (a [startNight, endNight] span), earliest first.
const stays = computed(() =>
  [...intents.value].sort((a, b) => a.startNight.localeCompare(b.startNight)),
)

function partyLabel(intent: AccommodationIntent): string {
  const parts: string[] = []
  if (intent.partyAdults) parts.push(t('accommodation.adults', { n: intent.partyAdults }, intent.partyAdults))
  if (intent.partyChildren) parts.push(t('accommodation.children', { n: intent.partyChildren }, intent.partyChildren))
  return parts.join(' · ')
}

// Status promotion flow for managers — a single lifecycle enum (Requested → Hold → Confirmed),
// with Declined as a terminal branch reached via the Decline action.
const STATUS_PROGRESSION: Partial<Record<AccommodationIntentStatus, AccommodationIntentStatus>> = {
  Requested: 'Hold',
  Hold: 'Confirmed',
}

function nextStatus(current: AccommodationIntentStatus): AccommodationIntentStatus | null {
  return STATUS_PROGRESSION[current] ?? null
}

async function handlePromote(intent: AccommodationIntent) {
  const next = nextStatus(intent.status)
  if (!next) return
  await promoteIntent(intent.id, intent.householdMemberId, intent.startNight, intent.endNight, next, intent.notes, intent.partyAdults, intent.partyChildren)
}

async function handleDecline(intent: AccommodationIntent) {
  await promoteIntent(intent.id, intent.householdMemberId, intent.startNight, intent.endNight, 'Declined', intent.notes, intent.partyAdults, intent.partyChildren)
}

// Accommodation display: bed inventory summary + effective area.
const bedSummary = computed(() => formatBedSummary(accommodation.value?.beds, t))
const areaLabel = computed(() => formatArea(accommodation.value?.effectiveAreaSqMeters ?? null))

// Request modal for members — reuses GsAccommodationRequestModal in free-date mode (no event context).
const showRequestModal = ref(false)
const requestLoading = ref(false)

// The member requests their own stay, so the modal's member picker has a single fixed option.
const selfMembers = computed<HouseholdMember[]>(() => {
  const id = memberStore.linkedMemberId
  const self = id ? memberMap.value.get(id) : undefined
  return self ? [self] : []
})

async function submitRequest(payload: {
  memberId: string
  startNight: string
  endNight: string
  status: AccommodationIntentStatus
  partyAdults: number | null
  partyChildren: number | null
  notes: string | null
}) {
  if (!memberStore.linkedHouseholdId) return
  requestLoading.value = true
  await requestIntent(
    memberStore.linkedHouseholdId,
    payload.memberId,
    payload.startNight,
    payload.endNight,
    payload.status,
    payload.notes,
    payload.partyAdults,
    payload.partyChildren,
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
      <UBadge v-if="bedSummary" color="neutral" variant="soft">
        {{ bedSummary }}
      </UBadge>
      <UBadge v-if="areaLabel" color="neutral" variant="soft">
        {{ areaLabel }}
      </UBadge>
    </div>

    <GsNotesSection :notes="accommodation?.notes" class="mb-6 max-w-lg" />

    <div class="flex items-center justify-between mb-4">
      <h2 class="text-base font-semibold">{{ t('accommodation.intents') }}</h2>
      <UButton
        v-if="memberStore.linkedMemberId && !isManagerOrAbove"
        size="sm"
        icon="i-heroicons-plus"
        @click="showRequestModal = true"
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
        @click="showRequestModal = true"
      >
        {{ t('accommodation.requestStay') }}
      </UButton>
    </GsEmptyState>

    <div v-else class="flex flex-col gap-3">
      <UCard
        v-for="intent in stays"
        :key="intent.id"
      >
        <div class="flex items-center gap-3 flex-wrap">
          <GsMemberAvatar :name="memberName(intent.householdMemberId)" size="sm" />
          <div class="flex-1 min-w-0">
            <p class="font-medium text-sm truncate">{{ memberName(intent.householdMemberId) }}</p>
            <p class="text-xs text-muted mt-0.5 flex items-center gap-1">
              <UIcon name="i-heroicons-calendar-days" class="size-3.5 shrink-0" />
              {{ formatDateRange(intent.startNight, intent.endNight) }}
            </p>
            <div class="flex flex-wrap gap-2 mt-1">
              <GsStatusBadge :status="intent.status" size="xs" />
              <span v-if="partyLabel(intent)" class="text-xs text-muted self-center">
                {{ partyLabel(intent) }}
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
                v-if="intent.status !== 'Declined'"
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

    <!-- Request stay modal (members) — shared modal in free-date mode (no event-day list) -->
    <GsAccommodationRequestModal
      v-model:open="showRequestModal"
      :accommodations="accommodation ? [accommodation] : []"
      :members="selfMembers"
      :event-days="[]"
      :default-member-id="memberStore.linkedMemberId"
      :loading="requestLoading"
      @submit="submitRequest"
    />
  </div>
</template>

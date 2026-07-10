<script setup lang="ts">
import { useMyStays, useMyStayActions } from '~/composables/useMyUpcoming'
import { useAllMembers } from '~/composables/useHouseholdMembers'
import { rangesOverlap } from '~/utils/dates'
import type { AccommodationIntent, AccommodationIntentStatus, MyStay } from '~/repositories/types'

const props = withDefaults(defineProps<{
  limit?: number
  /** When both dates are set, only stays overlapping [filterStart, filterEnd] are shown (event scope). */
  filterStart?: string | null
  filterEnd?: string | null
  showHeading?: boolean
  /** Render nothing when there are no (matching) stays, so a parent can show its own empty/CTA. */
  hideWhenEmpty?: boolean
}>(), {
  limit: 5,
  filterStart: null,
  filterEnd: null,
  showHeading: true,
  hideWhenEmpty: false,
})

const { t } = useI18n()
const toast = useToast()
const { formatDateRange } = useFormatDate()
const { stays, pending, refresh } = useMyStays()
const { memberMap } = useAllMembers()
const { submitting, deleting, loadIntent, updateStay, deleteStay } = useMyStayActions()

const filtered = computed(() => {
  if (props.filterStart && props.filterEnd) {
    return stays.value.filter(s => rangesOverlap(s.startNight, s.endNight, props.filterStart!, props.filterEnd!))
  }
  return stays.value
})

// A non-positive limit means "show all" (e.g. the event tab lists every overlapping stay).
const visible = computed(() => props.limit > 0 ? filtered.value.slice(0, props.limit) : filtered.value)

// ── Edit modal ──────────────────────────────────────────────────────────────
const modalOpen = ref(false)
const editStay = ref<MyStay | null>(null)
const editIntent = ref<AccommodationIntent | null>(null)
const loadingStayId = ref<string | null>(null)

// The member self-edits their own stay, so both pickers are locked to a single option.
const modalAccommodations = computed(() =>
  editStay.value ? [{ id: editStay.value.accommodationId, name: editStay.value.accommodationName }] : [])
const modalMembers = computed(() => {
  const stay = editStay.value
  if (!stay) return []
  // The picker is locked to the current member. If the map hasn't resolved, the modal still hydrates
  // memberId from editIntent, so the edit works — only the (locked) label would briefly be absent.
  const member = memberMap.value.get(stay.householdMemberId)
  return member ? [member] : []
})

async function openEditor(stay: MyStay) {
  if (loadingStayId.value) return
  loadingStayId.value = stay.id
  // MyStay omits notes, so fetch the full intent first to hydrate the form without losing fields.
  const full = await loadIntent(stay)
  loadingStayId.value = null
  if (!full) return
  editStay.value = stay
  editIntent.value = full // set before opening: the modal hydrates from editIntent on its open→true watch
  modalOpen.value = true
}

async function onSubmit(payload: {
  accommodationId: string
  memberId: string
  startNight: string
  endNight: string
  status: AccommodationIntentStatus
  partyAdults: number | null
  partyChildren: number | null
  notes: string | null
}) {
  const stay = editStay.value
  if (!stay) return
  const ok = await updateStay(stay, payload)
  if (ok) {
    modalOpen.value = false
    toast.add({ title: t('accommodations.stayUpdated'), color: 'success' })
    await refresh()
  }
}

async function onDelete() {
  const stay = editStay.value
  if (!stay) return
  const ok = await deleteStay(stay)
  if (ok) {
    modalOpen.value = false
    toast.add({ title: t('accommodations.stayDeleted'), color: 'success' })
    await refresh()
  }
}
</script>

<template>
  <div v-if="!hideWhenEmpty || visible.length">
    <h2 v-if="showHeading" class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
      {{ t('dashboard.myStays') }}
    </h2>

    <div v-if="pending && !hideWhenEmpty" class="rounded-lg border border-(--ui-border) bg-elevated p-6 text-center">
      <p class="text-sm text-muted">{{ t('common.loading') }}</p>
    </div>

    <div
      v-else-if="!visible.length && !hideWhenEmpty"
      class="rounded-lg border border-(--ui-border) bg-elevated p-6 flex flex-col items-center text-center gap-2"
    >
      <UIcon name="i-heroicons-home-modern" class="size-8 text-muted" />
      <p class="text-sm text-muted">{{ t('dashboard.noStays') }}</p>
      <UButton to="/app/accommodations" variant="link" size="xs">{{ t('dashboard.findStay') }}</UButton>
    </div>

    <ul v-else-if="visible.length" class="space-y-2">
      <li
        v-for="stay in visible"
        :key="stay.id"
        role="button"
        tabindex="0"
        :aria-label="t('accommodation.editStay')"
        class="rounded-lg border border-(--ui-border) bg-elevated p-3 flex items-center gap-3 cursor-pointer transition-all hover:ring-1 hover:ring-primary focus:outline-none focus-visible:ring-1 focus-visible:ring-primary"
        @click="openEditor(stay)"
        @keydown.enter.prevent="openEditor(stay)"
        @keydown.space.prevent="openEditor(stay)"
      >
        <UIcon
          :name="loadingStayId === stay.id ? 'i-heroicons-arrow-path' : 'i-heroicons-home-modern'"
          :class="['size-5 text-primary shrink-0', { 'animate-spin': loadingStayId === stay.id }]"
        />
        <div class="min-w-0 flex-1">
          <p class="text-sm font-medium truncate">{{ stay.accommodationName }}</p>
          <p class="text-xs text-muted truncate">{{ `${stay.propertyName} · ${formatDateRange(stay.startNight, stay.endNight)}` }}</p>
        </div>
        <GsStatusBadge :status="stay.status" size="xs" />
      </li>
    </ul>

    <GsAccommodationRequestModal
      v-model:open="modalOpen"
      :accommodations="modalAccommodations"
      :members="modalMembers"
      :event-days="[]"
      :default-member-id="editStay?.householdMemberId ?? null"
      :edit-intent="editIntent"
      :loading="submitting"
      :delete-loading="deleting"
      @submit="onSubmit"
      @delete="onDelete"
    />
  </div>
</template>

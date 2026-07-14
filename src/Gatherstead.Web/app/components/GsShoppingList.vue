<script setup lang="ts">
import type { ShoppingItem, ShoppingItemOrigin } from '~/repositories/types'
import type { ShoppingScope, ShoppingScopeOption, ShoppingSection } from '~/composables/useShoppingList'
import { REFRESH_INTERVAL_S, useShoppingList } from '~/composables/useShoppingList'
import { useTenantRole } from '~/composables/useTenantRole'
import { useAllMembers } from '~/composables/useHouseholdMembers'
import { today } from '~/utils/dates'

const props = defineProps<{ scope: ShoppingScope | null }>()
const { t } = useI18n()
const { formatDate } = useFormatDate()
const { isCoordinatorOrAbove } = useTenantRole()

const scopeRef = computed(() => props.scope)
const {
  sections, allItems, pending, updating, lastUpdatedAt, propertyId, mealScopeOptions,
  myMemberId, refresh, myIntent, remaining, canEditMealPlan, claim, provide, unclaim,
  createItem, updateItem, deleteItem,
} = useShoppingList(scopeRef)

const { nameFor: memberName } = useAllMembers()

// ── Mode (Shop = stripped-down in-store check-off; Edit = full CRUD for editors) ──
const mode = ref<'shop' | 'edit'>('shop')
const canEditAny = computed(() => canAdd.value || sections.value.some(canEditSection))

// ── Filters ──────────────────────────────────────────────────────────────────
const unfulfilledOnly = ref(false)
// 'all' = no date filter. A non-empty sentinel is required because USelect (reka-ui)
// forbids item values of '' (reserved for clearing the selection).
const selectedDate = ref<string>('all')
const selectedSource = ref<'all' | ShoppingItemOrigin>('all')

// Forward-looking by default: items whose need-by date has passed are "expired" and hidden
// unless Show past is on (or explicitly surfaced via a specific date). Undated items (property
// staples, undated event items) never expire.
const showPast = ref(false)
const todayIso = today()
function isExpired(item: ShoppingItem): boolean {
  return !!item.neededByDate && item.neededByDate < todayIso
}

const dateOptions = computed(() => {
  const dates = [...new Set(allItems.value.map(i => i.neededByDate).filter((d): d is string => !!d))]
    .filter(d => showPast.value || d >= todayIso)
    .sort()
  return [
    { label: t('shopping.allDates'), value: 'all' },
    ...dates.map(d => ({ label: formatDate(d), value: d })),
  ]
})

// If the selected date drops out of the list (e.g. Show past toggled off), fall back to All dates.
watch(dateOptions, (opts) => {
  if (!opts.some(o => o.value === selectedDate.value)) selectedDate.value = 'all'
})

// Only offer source options for origins actually present, and only when more than one exists.
const presentOrigins = computed(() => new Set(sections.value.map(s => s.origin)))
const sourceOptions = computed(() => [
  { label: t('shopping.allSources'), value: 'all' as const },
  ...(['Meal', 'Event', 'Property'] as const)
    .filter(o => presentOrigins.value.has(o))
    .map(o => ({ label: t(`shopping.origin.${o.toLowerCase()}`), value: o })),
])
const showSourceFilter = computed(() => sourceOptions.value.length > 2)

// Items the current member just acted on stay visible (even when "Unfulfilled only" would hide
// them) so they can refer back during a shop run; cleared on manual refresh and on mode switch.
const recentlyActed = ref<Set<string>>(new Set())

const visibleSections = computed<ShoppingSection[]>(() =>
  sections.value
    .filter(s => selectedSource.value === 'all' || s.origin === selectedSource.value)
    .map(s => ({
      ...s,
      items: s.items.filter(i =>
        (!unfulfilledOnly.value || i.status !== 'Covered' || recentlyActed.value.has(i.id))
        && (selectedDate.value === 'all'
          ? (showPast.value || !isExpired(i) || recentlyActed.value.has(i.id))
          : i.neededByDate === selectedDate.value),
      ),
    }))
    // When filtering, drop empty sections; otherwise keep the always-present scope sections.
    .filter(s => s.items.length > 0
      || (!unfulfilledOnly.value && selectedDate.value === 'all'
        && selectedSource.value === 'all' && showPast.value)),
)

function doRefresh() {
  recentlyActed.value = new Set()
  void refresh()
}
watch(mode, () => { recentlyActed.value = new Set() })

// ── Shop-mode actions (record the item so it stays visible after committing) ──
function markActed(itemId: string) {
  recentlyActed.value = new Set(recentlyActed.value).add(itemId)
}
async function shopCover(item: ShoppingItem, quantity: number | null) {
  await provide(item, quantity)
  markActed(item.id)
}
async function shopClaim(item: ShoppingItem, quantity: number | null) {
  await claim(item, quantity)
  markActed(item.id)
}
async function shopUndo(item: ShoppingItem) {
  await unclaim(item)
  markActed(item.id)
}

// ── Create scopes offered by the modal (respecting who may create where) ──────
const createScopes = computed<ShoppingScopeOption[]>(() => {
  const s = props.scope
  if (!s) return []
  const opts: ShoppingScopeOption[] = []
  if (isCoordinatorOrAbove.value) {
    if (s.kind === 'event' && s.eventId) opts.push({ label: t('shopping.eventSupplies'), eventId: s.eventId })
    if (propertyId.value) opts.push({ label: t('shopping.propertySupplies'), propertyId: propertyId.value })
    opts.push(...mealScopeOptions.value)
  }
  else {
    // Volunteers may only add to meal lists they are responsible for.
    opts.push(...mealScopeOptions.value.filter(o => o.mealPlanId && canEditMealPlan(o.mealPlanId)))
  }
  return opts
})
const canAdd = computed(() => createScopes.value.length > 0)

function canEditSection(section: ShoppingSection): boolean {
  return section.origin === 'Meal' ? canEditMealPlan(section.planId) : isCoordinatorOrAbove.value
}

// ── Staleness label (ticks so "updated Ns ago" stays current) ────────────────
const now = ref(Date.now())
let tick: ReturnType<typeof setInterval> | null = null
onMounted(() => { tick = setInterval(() => { now.value = Date.now() }, 15_000) })
onUnmounted(() => { if (tick) clearInterval(tick) })

const staleSeconds = computed(() => lastUpdatedAt.value ? Math.floor((now.value - lastUpdatedAt.value) / 1000) : 0)
// Only warn once a full auto-refresh cycle has clearly been missed (2× the interval), rather than
// nagging one grace period after a single skipped/slow poll. Derived from the refresh rate so the
// two stay in sync.
const staleThresholdSeconds = (REFRESH_INTERVAL_S * 2)
const isStale = computed(() => staleSeconds.value >= staleThresholdSeconds)
const updatedLabel = computed(() => {
  if (!lastUpdatedAt.value) return ''
  const mins = Math.floor(staleSeconds.value / 60)
  return mins >= 1
    ? t('shopping.updatedMinutesAgo', { count: mins })
    : t('shopping.updatedSecondsAgo', { count: staleSeconds.value })
})

// ── Modal wiring ─────────────────────────────────────────────────────────────
const modalOpen = ref(false)
const editing = ref<ShoppingItem | null>(null)

function openAdd() {
  editing.value = null
  modalOpen.value = true
}
function openEdit(item: ShoppingItem) {
  editing.value = item
  modalOpen.value = true
}

// ── Delete confirmation ──────────────────────────────────────────────────────
const deleteModalOpen = ref(false)
const pendingDelete = ref<ShoppingItem | null>(null)
function askDelete(item: ShoppingItem) {
  pendingDelete.value = item
  deleteModalOpen.value = true
}
const deleteWarning = computed(() =>
  pendingDelete.value && pendingDelete.value.status !== 'Needed'
    ? t('shopping.deleteItemClaimedWarning')
    : t('shopping.deleteItemConfirm'),
)
function confirmDelete() {
  if (pendingDelete.value) void deleteItem(pendingDelete.value.id)
}

const statusColor: Record<string, 'neutral' | 'warning' | 'success'> = {
  Needed: 'neutral', Claimed: 'warning', Covered: 'success',
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between gap-3 flex-wrap">
      <div class="flex items-center gap-3 flex-wrap">
        <UFieldGroup v-if="canEditAny" size="sm">
          <UButton :variant="mode === 'shop' ? 'solid' : 'outline'" icon="i-heroicons-shopping-cart" @click="() => { mode = 'shop' }">
            {{ t('shopping.shopMode') }}
          </UButton>
          <UButton :variant="mode === 'edit' ? 'solid' : 'outline'" icon="i-heroicons-pencil-square" @click="() => { mode = 'edit' }">
            {{ t('shopping.editMode') }}
          </UButton>
        </UFieldGroup>
        <USelect
          v-model="selectedDate"
          :items="dateOptions"
          size="sm"
          :icon="'i-heroicons-calendar-days'"
          class="min-w-40"
        />
        <USelect
          v-if="showSourceFilter"
          v-model="selectedSource"
          :items="sourceOptions"
          size="sm"
          :icon="'i-heroicons-funnel'"
          class="min-w-36"
        />
        <UCheckbox v-model="unfulfilledOnly" :label="t('shopping.unfulfilledOnly')" />
        <UCheckbox v-model="showPast" :label="t('shopping.showPast')" />
      </div>
      <div class="flex items-center gap-3">
        <span v-if="lastUpdatedAt" class="text-sm text-muted">{{ updatedLabel }}</span>
        <UButton
          variant="ghost"
          size="xs"
          icon="i-heroicons-arrow-path"
          :loading="pending"
          @click="doRefresh"
        >
          {{ t('shopping.refresh') }}
        </UButton>
        <UButton v-if="mode === 'edit' && canAdd" icon="i-heroicons-plus" size="sm" @click="openAdd">
          {{ t('shopping.addItem') }}
        </UButton>
      </div>
    </div>

    <UAlert
      v-if="isStale"
      icon="i-heroicons-exclamation-triangle"
      color="warning"
      variant="soft"
      :title="t('shopping.staleTitle')"
      :description="t('shopping.staleDescription')"
    >
      <template #actions>
        <UButton color="warning" size="xs" :loading="pending" @click="doRefresh">
          {{ t('shopping.refresh') }}
        </UButton>
      </template>
    </UAlert>

    <p v-if="visibleSections.length === 0" class="text-sm text-muted">
      {{ t('shopping.emptySection') }}
    </p>

    <div v-for="section in visibleSections" :key="section.id" class="space-y-2">
      <div class="flex items-center gap-2">
        <UBadge
          :color="section.origin === 'Meal' ? 'primary' : 'neutral'"
          variant="subtle"
          size="sm"
        >
          {{ t(`shopping.origin.${section.origin.toLowerCase()}`) }}
        </UBadge>
        <h3 class="font-medium">{{ section.title }}</h3>
        <span v-if="section.subtitle" class="text-sm text-muted">{{ section.subtitle }}</span>
      </div>

      <p v-if="section.items.length === 0" class="text-sm text-muted pl-1">
        {{ t('shopping.emptySection') }}
      </p>

      <!-- Shop mode: stripped-down, few-tap check-off rows. -->
      <ul v-if="mode === 'shop'" class="space-y-2">
        <GsShoppingShopItem
          v-for="item in section.items"
          :key="item.id"
          :item="item"
          :my-intent="myIntent(item)"
          :busy="updating.includes(item.id)"
          :can-act="!!myMemberId"
          :member-name="memberName"
          @claim="qty => shopClaim(item, qty)"
          @cover="qty => shopCover(item, qty)"
          @undo="shopUndo(item)"
        />
      </ul>

      <!-- Edit mode: full item management for editors. -->
      <ul v-else class="space-y-2">
        <li
          v-for="item in section.items"
          :key="item.id"
          class="flex items-start justify-between gap-3 rounded-md border border-default p-3"
        >
          <div class="min-w-0 space-y-1">
            <div class="flex items-center gap-2 flex-wrap">
              <span class="font-medium">{{ item.name }}</span>
              <UBadge :color="statusColor[item.status ?? 'Needed']" variant="subtle" size="xs">
                {{ t(`shopping.status.${(item.status ?? 'Needed').toLowerCase()}`) }}
              </UBadge>
              <UBadge v-if="item.category" color="neutral" variant="outline" size="xs">{{ item.category }}</UBadge>
            </div>
            <div v-if="item.quantityNeeded != null" class="text-sm text-muted">
              {{ t('shopping.quantitySummary', {
                needed: item.quantityNeeded,
                provided: item.quantityProvided ?? 0,
                unit: item.unit ?? '',
              }) }}
              <span v-if="remaining(item) && remaining(item)! > 0" class="text-warning ml-1">
                {{ t('shopping.remaining', { count: remaining(item), unit: item.unit ?? '' }) }}
              </span>
            </div>
            <p v-if="item.notes" class="text-sm text-muted">{{ item.notes }}</p>
          </div>

          <div v-if="canEditSection(section)" class="flex items-center gap-1 shrink-0">
            <UButton variant="ghost" size="xs" icon="i-heroicons-pencil" @click="openEdit(item)" />
            <UButton variant="ghost" size="xs" color="error" icon="i-heroicons-trash" @click="askDelete(item)" />
          </div>
        </li>
      </ul>
    </div>

    <GsShoppingItemModal
      v-model:open="modalOpen"
      :item="editing"
      :scope-options="editing ? undefined : createScopes"
      :busy="updating.includes('new') || (editing ? updating.includes(editing.id) : false)"
      @create="input => createItem(input)"
      @update="payload => updateItem(payload.itemId, payload.input)"
    />

    <GsConfirmModal
      v-model:open="deleteModalOpen"
      :title="t('shopping.deleteItemTitle')"
      :description="deleteWarning"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDelete"
    />
  </div>
</template>

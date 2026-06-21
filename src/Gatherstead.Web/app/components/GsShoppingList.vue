<script setup lang="ts">
import type { ShoppingItem } from '~/repositories/types'
import type { ShoppingScopeOption } from '~/composables/useShoppingList'
import { useShoppingList } from '~/composables/useShoppingList'
import { useCurrentMemberStore } from '~/stores/member'

const props = defineProps<{ eventId: string }>()
const { t } = useI18n()
const memberStore = useCurrentMemberStore()

const eventIdRef = toRef(props, 'eventId')
const {
  sections, pending, updating, lastUpdatedAt, propertyId, mealScopeOptions,
  refresh, setFulfillment, createItem, updateItem, deleteItem,
} = useShoppingList(eventIdRef)

const hideCovered = ref(false)

const visibleSections = computed(() =>
  sections.value
    .map(s => ({ ...s, items: hideCovered.value ? s.items.filter(i => i.status !== 'Covered') : s.items }))
    .filter(s => s.items.length > 0 || s.origin === 'Event' || s.origin === 'Property'),
)

// Scope options offered by the "Add item" modal: the event itself, any meal occurrence,
// and (when resolvable) the event's property.
const createScopes = computed<ShoppingScopeOption[]>(() => {
  const opts: ShoppingScopeOption[] = [{ label: t('shopping.eventSupplies'), eventId: props.eventId }]
  if (propertyId.value) opts.push({ label: t('shopping.propertySupplies'), propertyId: propertyId.value })
  return [...opts, ...mealScopeOptions.value]
})

// ── Staleness label (ticks so "updated Ns ago" stays current) ────────────────
const now = ref(Date.now())
let tick: ReturnType<typeof setInterval> | null = null
onMounted(() => { tick = setInterval(() => { now.value = Date.now() }, 15_000) })
onUnmounted(() => { if (tick) clearInterval(tick) })

const staleSeconds = computed(() => lastUpdatedAt.value ? Math.floor((now.value - lastUpdatedAt.value) / 1000) : 0)
const isStale = computed(() => staleSeconds.value >= 90)
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

function remaining(item: ShoppingItem): number | null {
  if (item.quantityNeeded == null) return null
  return item.quantityNeeded - (item.quantityProvided ?? 0)
}

function claim(item: ShoppingItem) {
  setFulfillment(item, 'Claimed', item.quantityProvided ?? null, memberStore.linkedMemberId)
}
function markCovered(item: ShoppingItem) {
  setFulfillment(item, 'Covered', item.quantityNeeded ?? item.quantityProvided ?? null, item.claimedByMemberId ?? memberStore.linkedMemberId)
}
function reopen(item: ShoppingItem) {
  setFulfillment(item, 'Needed', item.quantityProvided ?? null, null)
}
function setProvided(item: ShoppingItem, value: string) {
  const n = value.trim() ? Number(value) : null
  const qty = n != null && Number.isFinite(n) ? n : null
  const status = qty != null && item.quantityNeeded != null && qty >= item.quantityNeeded ? 'Covered' : item.status ?? 'Claimed'
  setFulfillment(item, status, qty, item.claimedByMemberId ?? memberStore.linkedMemberId)
}

const statusColor: Record<string, 'neutral' | 'warning' | 'success'> = {
  Needed: 'neutral', Claimed: 'warning', Covered: 'success',
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between gap-3 flex-wrap">
      <div class="flex items-center gap-3 text-sm text-muted">
        <span v-if="lastUpdatedAt">{{ updatedLabel }}</span>
        <UButton
          variant="ghost"
          size="xs"
          icon="i-heroicons-arrow-path"
          :loading="pending"
          @click="refresh"
        >
          {{ t('shopping.refresh') }}
        </UButton>
      </div>
      <div class="flex items-center gap-3">
        <UCheckbox v-model="hideCovered" :label="t('shopping.hideCovered')" />
        <UButton icon="i-heroicons-plus" size="sm" @click="openAdd">
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
        <UButton color="warning" size="xs" :loading="pending" @click="refresh">
          {{ t('shopping.refresh') }}
        </UButton>
      </template>
    </UAlert>

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

      <ul class="space-y-2">
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

          <div class="flex items-center gap-1 shrink-0">
            <UInput
              v-if="item.quantityNeeded != null"
              :model-value="item.quantityProvided != null ? String(item.quantityProvided) : ''"
              type="number"
              step="any"
              size="xs"
              class="w-20"
              :placeholder="t('shopping.got')"
              @change="setProvided(item, ($event.target as HTMLInputElement).value)"
            />
            <UButton
              v-if="item.status !== 'Claimed'"
              variant="ghost"
              size="xs"
              :loading="updating.includes(item.id)"
              @click="claim(item)"
            >
              {{ t('shopping.claim') }}
            </UButton>
            <UButton
              v-if="item.status !== 'Covered'"
              variant="ghost"
              size="xs"
              color="success"
              icon="i-heroicons-check"
              :loading="updating.includes(item.id)"
              @click="markCovered(item)"
            >
              {{ t('shopping.covered') }}
            </UButton>
            <UButton
              v-else
              variant="ghost"
              size="xs"
              icon="i-heroicons-arrow-uturn-left"
              :loading="updating.includes(item.id)"
              @click="reopen(item)"
            >
              {{ t('shopping.reopen') }}
            </UButton>

            <GsRoleGate min-role="Coordinator">
              <UButton variant="ghost" size="xs" icon="i-heroicons-pencil" @click="openEdit(item)" />
              <UButton variant="ghost" size="xs" color="error" icon="i-heroicons-trash" @click="deleteItem(item.id)" />
            </GsRoleGate>
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
  </div>
</template>

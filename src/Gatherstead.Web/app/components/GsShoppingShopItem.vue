<script setup lang="ts">
import type { ShoppingItem, ShoppingItemIntent } from '~/repositories/types'

const props = defineProps<{
  item: ShoppingItem
  myIntent: ShoppingItemIntent | null
  busy?: boolean
  canAct?: boolean
  // Resolves a household member id to a display name (for the investigative "who" reveal).
  memberName: (id: string) => string
}>()

const emit = defineEmits<{
  claim: [quantity: number | null]
  cover: [quantity: number | null]
  undo: []
}>()

const { t } = useI18n()

const expanded = ref(false)
const showWho = ref(false)
// Typed string for UInput's modelValue, but type="number" coerces it to a number at
// runtime — parseQty() handles both rather than assuming a string.
const draftQty = ref('')

const remaining = computed(() => {
  if (props.item.quantityNeeded == null) return null
  return props.item.quantityNeeded - (props.item.quantityProvided ?? 0)
})

const isCovered = computed(() => props.item.status === 'Covered')
const isHandled = computed(() => props.item.status !== 'Needed')

// Status drives a leading icon only — never the row's position in the list.
const statusIcon = computed(() =>
  isCovered.value ? 'i-heroicons-check-circle-solid'
    : isHandled.value ? 'i-heroicons-clock'
      : 'i-heroicons-square-2-stack',
)
const statusIconClass = computed(() =>
  isCovered.value ? 'text-success' : isHandled.value ? 'text-warning' : 'text-dimmed',
)

const contributors = computed(() => props.item.intents ?? [])

function contributorLabel(intent: ShoppingItemIntent): string {
  const status = t(`shopping.status.${intent.status === 'Provided' ? 'covered' : 'claimed'}`)
  if (intent.quantity == null) return status
  return `${status} · ${intent.quantity} ${props.item.unit ?? ''}`.trim()
}

function toggle() {
  expanded.value = !expanded.value
  if (expanded.value) {
    // Default to the member's existing pledge, else the full remaining amount — the common case.
    draftQty.value = props.myIntent?.quantity != null
      ? String(props.myIntent.quantity)
      : remaining.value != null
        ? String(remaining.value)
        : ''
  }
}

function parseQty(): number | null {
  const raw = draftQty.value
  if (raw === '') return null
  const n = Number(raw)
  return Number.isFinite(n) ? n : null
}

function onClaim() {
  emit('claim', parseQty())
  expanded.value = false
}
function onCover() {
  emit('cover', parseQty())
  expanded.value = false
}
function onUndo() {
  emit('undo')
  expanded.value = false
}
</script>

<template>
  <li class="rounded-md border border-default" :class="{ 'opacity-70': isCovered }">
    <div
      class="flex items-center justify-between gap-3 p-3 cursor-pointer select-none"
      role="button"
      tabindex="0"
      @click="toggle"
      @keydown.enter.prevent="toggle"
      @keydown.space.prevent="toggle"
    >
      <div class="flex items-center gap-2 min-w-0">
        <UIcon :name="statusIcon" class="shrink-0" :class="statusIconClass" />
        <span class="font-medium truncate" :class="{ 'line-through text-muted': isCovered }">
          {{ item.name }}
        </span>
      </div>
      <div class="flex items-center gap-2 shrink-0 text-sm">
        <span v-if="remaining != null && remaining > 0" class="text-muted">
          {{ t('shopping.remaining', { count: remaining, unit: item.unit ?? '' }) }}
        </span>
        <UIcon
          :name="expanded ? 'i-heroicons-chevron-up' : 'i-heroicons-chevron-down'"
          class="text-dimmed"
        />
      </div>
    </div>

    <div v-if="expanded" class="border-t border-default p-3 space-y-3">
      <template v-if="canAct">
        <div class="flex items-end gap-2 flex-wrap">
          <UFormField v-if="item.quantityNeeded != null" :label="t('shopping.quantity')">
            <UInput v-model="draftQty" type="number" step="any" size="sm" class="w-24" />
          </UFormField>
          <UButton
            color="success"
            icon="i-heroicons-check"
            size="sm"
            :loading="busy"
            @click="onCover"
          >
            {{ t('shopping.gotIt') }}
          </UButton>
          <UButton
            variant="soft"
            size="sm"
            :loading="busy"
            @click="onClaim"
          >
            {{ t('shopping.claim') }}
          </UButton>
          <UButton
            v-if="myIntent"
            variant="ghost"
            size="sm"
            icon="i-heroicons-arrow-uturn-left"
            :loading="busy"
            @click="onUndo"
          >
            {{ t('shopping.undo') }}
          </UButton>
        </div>
      </template>
      <p v-else class="text-sm text-muted">{{ t('shopping.noMemberLink') }}</p>

      <p v-if="item.notes" class="text-sm text-muted">{{ item.notes }}</p>

      <div>
        <UButton
          variant="link"
          size="xs"
          icon="i-heroicons-user-group"
          class="px-0"
          @click="showWho = !showWho"
        >
          {{ t('shopping.whoCovering') }}
        </UButton>
        <ul v-if="showWho" class="mt-2 space-y-1">
          <li v-if="contributors.length === 0" class="text-sm text-muted">
            {{ t('shopping.emptySection') }}
          </li>
          <li
            v-for="intent in contributors"
            :key="intent.id"
            class="flex items-center gap-2 text-sm"
          >
            <GsMemberAvatar :name="memberName(intent.householdMemberId!)" size="xs" />
            <span>{{ memberName(intent.householdMemberId!) }}</span>
            <span class="text-muted">{{ contributorLabel(intent) }}</span>
          </li>
        </ul>
      </div>
    </div>
  </li>
</template>

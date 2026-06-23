<script setup lang="ts">
import type { ShoppingItem } from '~/repositories/types'
import type { CreateShoppingItemInput, UpdateShoppingItemInput } from '~/repositories/interfaces'
import type { ShoppingScopeOption } from '~/composables/useShoppingList'

const props = defineProps<{
  item?: ShoppingItem | null
  scopeOptions?: ShoppingScopeOption[]
  busy?: boolean
}>()

const emit = defineEmits<{
  create: [CreateShoppingItemInput]
  update: [{ itemId: string, input: UpdateShoppingItemInput }]
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const isEdit = computed(() => !!props.item)
const scopes = computed(() => props.scopeOptions ?? [])

// Once an item is claimed/covered, renaming it would silently change what others pledged
// against (e.g. "vegetable oil" → "olive oil"), so the name is locked; editors delete + recreate.
const nameLocked = computed(() => isEdit.value && props.item?.status !== 'Needed')

const form = reactive({
  scopeIndex: 0,
  name: '',
  quantityNeeded: '',
  unit: '',
  neededByDate: '',
  category: '',
  notes: '',
})
const errors = reactive({ name: '' })

// A meal item's need date is fixed to its meal's day, so the field is hidden for meal scope.
const selectedScopeIsMeal = computed(() =>
  isEdit.value ? props.item?.origin === 'Meal' : !!scopes.value[form.scopeIndex]?.mealPlanId,
)

const scopeSelectItems = computed(() =>
  scopes.value.map((s, i) => ({ label: s.label, value: i })),
)

function reset() {
  const it = props.item
  form.scopeIndex = 0
  form.name = it?.name ?? ''
  form.quantityNeeded = it?.quantityNeeded != null ? String(it.quantityNeeded) : ''
  form.unit = it?.unit ?? ''
  form.neededByDate = it?.neededByDate ?? ''
  form.category = it?.category ?? ''
  form.notes = it?.notes ?? ''
  errors.name = ''
}

watch(open, (isOpen) => { if (isOpen) reset() })

function parseQuantity(): number | null {
  // A type="number" UInput hands back a number once edited, but reset() seeds a string —
  // coerce so .trim() is always safe.
  const trimmed = String(form.quantityNeeded ?? '').trim()
  if (!trimmed) return null
  const n = Number(trimmed)
  return Number.isFinite(n) ? n : null
}

function submit() {
  errors.name = form.name.trim() ? '' : t('validation.required', { field: t('shopping.itemName') })
  if (errors.name) return

  const base = {
    name: form.name.trim(),
    quantityNeeded: parseQuantity(),
    unit: form.unit.trim() || null,
    neededByDate: selectedScopeIsMeal.value ? null : (form.neededByDate || null),
    category: form.category.trim() || null,
    notes: form.notes.trim() || null,
  }

  if (isEdit.value && props.item) {
    emit('update', { itemId: props.item.id, input: base })
  }
  else {
    const scope = scopes.value[form.scopeIndex]
    emit('create', {
      ...base,
      propertyId: scope?.propertyId ?? null,
      eventId: scope?.eventId ?? null,
      mealPlanId: scope?.mealPlanId ?? null,
    })
  }
  open.value = false
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="isEdit ? t('shopping.editItem') : t('shopping.addItem')"
  >
    <template #body>
      <div class="space-y-5">
        <UFormField v-if="!isEdit && scopeSelectItems.length > 1" :label="t('shopping.list')">
          <USelect v-model="form.scopeIndex" :items="scopeSelectItems" class="w-full" />
        </UFormField>

        <UFormField
          :label="t('shopping.itemName')"
          :error="errors.name || undefined"
          :description="nameLocked ? t('shopping.renameLocked') : undefined"
          :required="!nameLocked"
        >
          <UInput
            v-model="form.name"
            :placeholder="t('shopping.itemNamePlaceholder')"
            :disabled="nameLocked"
            class="w-full"
          />
        </UFormField>

        <div class="flex gap-3">
          <UFormField :label="t('shopping.quantity')" class="flex-1">
            <UInput v-model="form.quantityNeeded" type="number" step="any" :placeholder="t('shopping.quantityPlaceholder')" class="w-full" />
          </UFormField>
          <UFormField :label="t('shopping.unit')" class="flex-1">
            <UInput v-model="form.unit" :placeholder="t('shopping.unitPlaceholder')" class="w-full" />
          </UFormField>
        </div>

        <UFormField v-if="!selectedScopeIsMeal" :label="t('shopping.neededBy')">
          <UInput v-model="form.neededByDate" type="date" class="w-full" />
        </UFormField>

        <UFormField :label="t('shopping.category')">
          <UInput v-model="form.category" :placeholder="t('shopping.categoryPlaceholder')" class="w-full" />
        </UFormField>

        <UFormField :label="t('common.notes')">
          <UTextarea v-model="form.notes" class="w-full" />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <GsFormFooter
        :submit-label="isEdit ? t('common.save') : t('common.create')"
        :loading="busy"
        @submit="submit"
        @cancel="open = false"
      />
    </template>
  </UModal>
</template>

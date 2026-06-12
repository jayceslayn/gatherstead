<script setup lang="ts">
import { useEquipment, useEquipmentActions } from '~/composables/useEquipment'
import { useProperties } from '~/composables/useProperties'
import { useTenantRole } from '~/composables/useTenantRole'
import type { EquipmentSummary } from '~/repositories/types'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const { isManagerOrAbove } = useTenantRole()
const { equipment, pending, refresh } = useEquipment()
const { properties, pending: propertiesPending } = useProperties()
const { updating, deleteEquipment } = useEquipmentActions(refresh)

const propertyItems = computed(() => properties.value.map(p => ({ label: p.name, value: p.id })))
function propertyName(id: string | null): string {
  if (!id) return t('equipment.noProperty')
  return properties.value.find(p => p.id === id)?.name ?? t('equipment.unassignedProperty')
}

// === Search + property filter ===
const search = ref('')
const propertyFilter = ref<string>('all') // 'all' | 'none' | <propertyId>
const propertyFilterItems = computed(() => [
  { label: t('equipment.allProperties'), value: 'all' },
  ...propertyItems.value,
  { label: t('equipment.noProperty'), value: 'none' },
])

const filteredEquipment = computed(() => {
  const q = search.value.trim().toLowerCase()
  return equipment.value.filter((item) => {
    const matchesName = !q || item.name.toLowerCase().includes(q)
    const matchesProperty
      = propertyFilter.value === 'all'
        ? true
        : propertyFilter.value === 'none'
          ? !item.propertyId
          : item.propertyId === propertyFilter.value
    return matchesName && matchesProperty
  })
})

const showModal = ref(false)
const editing = ref<EquipmentSummary | null>(null)
const toDelete = ref<EquipmentSummary | null>(null)

function openCreate() {
  editing.value = null
  showModal.value = true
}
function openEdit(item: EquipmentSummary) {
  editing.value = item
  showModal.value = true
}
async function confirmDelete() {
  const item = toDelete.value
  toDelete.value = null
  if (item) await deleteEquipment(item.id)
}
</script>

<template>
  <div>
    <GsPageHeader :title="t('equipment.title')">
      <GsRoleGate min-role="Manager">
        <UButton icon="i-heroicons-plus" size="sm" @click="openCreate">
          {{ t('equipment.createTitle') }}
        </UButton>
      </GsRoleGate>
    </GsPageHeader>

    <p class="text-sm text-muted mb-6">{{ t('equipment.subtitle') }}</p>

    <div v-if="pending || propertiesPending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="!equipment.length"
      icon="i-heroicons-wrench-screwdriver"
      :title="t('equipment.noEquipment')"
      :description="isManagerOrAbove ? t('equipment.noEquipmentHintManager') : t('equipment.noEquipmentHintMember')"
    >
      <GsRoleGate min-role="Manager">
        <UButton icon="i-heroicons-plus" @click="openCreate">
          {{ t('equipment.createTitle') }}
        </UButton>
      </GsRoleGate>
    </GsEmptyState>

    <template v-else>
      <!-- Toolbar: search by name + filter by property -->
      <div class="flex flex-col sm:flex-row gap-3 mb-4">
        <UInput
          v-model="search"
          icon="i-heroicons-magnifying-glass"
          :placeholder="t('equipment.searchPlaceholder')"
          class="flex-1"
        />
        <USelect
          v-model="propertyFilter"
          :items="propertyFilterItems"
          icon="i-heroicons-funnel"
          class="sm:w-56"
        />
      </div>

      <GsEmptyState
        v-if="!filteredEquipment.length"
        icon="i-heroicons-magnifying-glass"
        :title="t('equipment.noMatches')"
      />

      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <UCard v-for="item in filteredEquipment" :key="item.id">
          <div class="flex items-start gap-3">
            <div class="rounded-lg bg-primary/10 p-2 shrink-0">
              <UIcon name="i-heroicons-wrench-screwdriver" class="size-5 text-primary" />
            </div>
            <div class="min-w-0 flex-1">
              <p class="font-semibold truncate">{{ item.name }}</p>
              <p class="text-xs text-muted truncate">{{ propertyName(item.propertyId) }}</p>
            </div>
            <GsRoleGate min-role="Manager">
              <div class="flex items-center shrink-0">
                <UButton
                  variant="ghost"
                  size="xs"
                  icon="i-heroicons-pencil"
                  :aria-label="t('common.edit')"
                  @click="openEdit(item)"
                />
                <UButton
                  color="error"
                  variant="ghost"
                  size="xs"
                  icon="i-heroicons-trash"
                  :aria-label="t('common.delete')"
                  :loading="updating.includes(item.id)"
                  @click="toDelete = item"
                />
              </div>
            </GsRoleGate>
          </div>

          <p v-if="item.notes" class="text-sm text-muted mt-3 break-words whitespace-pre-wrap">{{ item.notes }}</p>

          <div v-if="item.attributes.length" class="mt-3 pt-3 border-t border-default">
            <GsAttributeList :attributes="item.attributes" />
          </div>
        </UCard>
      </div>
    </template>

    <GsEquipmentModal
      v-model:open="showModal"
      :equipment="editing"
      :property-items="propertyItems"
      :refresh="refresh"
    />

    <GsConfirmModal
      :open="!!toDelete"
      :title="t('equipment.deleteTitle')"
      :description="t('equipment.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @update:open="(val: boolean) => { if (!val) toDelete = null }"
      @confirm="confirmDelete"
    />
  </div>
</template>

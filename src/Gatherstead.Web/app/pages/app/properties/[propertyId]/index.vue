<script setup lang="ts">
import { useProperty, usePropertyActions } from '~/composables/useProperties'
import { useAccommodations, useAccommodationActions } from '~/composables/useAccommodations'
import { useTenantRole } from '~/composables/useTenantRole'
import type { AccommodationSummary, AccommodationType } from '~/repositories/types'
import { ACCOMMODATION_TYPE_ORDER } from '~/utils/sorting'

definePageMeta({ layout: 'default' })

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const { isManagerOrAbove } = useTenantRole()

const propertyId = computed(() => route.params.propertyId as string)
const { property, pending: propertyPending, refresh } = useProperty(propertyId)
const { accommodations, pending: accommodationsPending, refresh: refreshAccommodations } = useAccommodations(propertyId)
const { deleteProperty } = usePropertyActions(refresh)
const { updating: accommodationUpdating, deleteAccommodation } = useAccommodationActions(propertyId, refreshAccommodations)

const showEdit = ref(false)
const showDeleteConfirm = ref(false)

async function confirmDelete() {
  showDeleteConfirm.value = false
  await deleteProperty(propertyId.value)
  await router.push('/app/properties')
}

function onPropertyModalDelete() {
  showEdit.value = false
  showDeleteConfirm.value = true
}

const grouped = computed(() => {
  const map = new Map<AccommodationType, AccommodationSummary[]>()
  for (const type of ACCOMMODATION_TYPE_ORDER) {
    const group = accommodations.value.filter(a => a.type === type)
    if (group.length) map.set(type, group)
  }
  return map
})

function typeLabel(type: AccommodationType): string {
  const key = type.charAt(0).toLowerCase() + type.slice(1)
  return t(`accommodation.types.${key}`)
}

// Accommodation modal
const showAccommodationModal = ref(false)
const editingAccommodation = ref<AccommodationSummary | null>(null)
const defaultAccommodationType = ref<AccommodationType>('Bedroom')
const toDeleteAccommodation = ref<AccommodationSummary | null>(null)
const showAccommodationDeleteConfirm = ref(false)

function openCreateAccommodation(type: AccommodationType) {
  editingAccommodation.value = null
  defaultAccommodationType.value = type
  showAccommodationModal.value = true
}

function openEditAccommodation(accommodation: AccommodationSummary) {
  editingAccommodation.value = accommodation
  showAccommodationModal.value = true
}

function onAccommodationModalDelete(accommodation: AccommodationSummary) {
  toDeleteAccommodation.value = accommodation
  showAccommodationModal.value = false
  showAccommodationDeleteConfirm.value = true
}

async function confirmDeleteAccommodation() {
  const acc = toDeleteAccommodation.value
  toDeleteAccommodation.value = null
  showAccommodationDeleteConfirm.value = false
  if (acc) await deleteAccommodation(acc.id)
}
</script>

<template>
  <div>
    <div v-if="propertyPending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <template v-else-if="property">
      <GsBreadcrumb
        :items="[
          { label: t('property.title'), to: '/app/properties' },
          { label: property.name },
        ]"
      />

      <GsPageHeader :title="property.name">
        <GsRoleGate min-role="Manager">
          <div class="flex items-center gap-2">
            <UButton variant="outline" size="sm" icon="i-heroicons-pencil" @click="() => { showEdit = true }">
              {{ t('common.edit') }}
            </UButton>
            <UButton size="sm" icon="i-heroicons-plus" @click="openCreateAccommodation('Bedroom')">
              {{ t('accommodation.createTitle') }}
            </UButton>
          </div>
        </GsRoleGate>
      </GsPageHeader>

      <GsNotesSection :notes="property.notes" class="mb-6 max-w-lg" />

      <GsAttributeSection :attributes="property.attributes" class="mb-6 max-w-lg" />

      <div v-if="accommodationsPending" class="py-8 text-center">
        <p class="text-muted">{{ t('common.loading') }}</p>
      </div>

      <GsEmptyState
        v-else-if="!accommodations.length"
        icon="i-heroicons-home"
        :title="t('property.noAccommodations')"
        :description="isManagerOrAbove ? t('property.noAccommodationsHintManager') : undefined"
      />

      <div v-else class="space-y-6">
        <div
          v-for="[type, group] in grouped"
          :key="type"
        >
          <h2 class="text-sm font-semibold text-muted uppercase tracking-wide mb-3">
            {{ typeLabel(type) }}
          </h2>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            <GsAccommodationCard
              v-for="accommodation in group"
              :key="accommodation.id"
              :accommodation="accommodation"
              :link-to="`/app/properties/${property.id}/accommodations/${accommodation.id}/intents`"
            >
              <template v-if="isManagerOrAbove" #actions>
                <UButton
                  variant="ghost"
                  size="xs"
                  icon="i-heroicons-pencil"
                  :aria-label="t('common.edit')"
                  :loading="accommodationUpdating.includes(accommodation.id)"
                  @click.prevent="openEditAccommodation(accommodation)"
                />
              </template>
            </GsAccommodationCard>
          </div>
        </div>
      </div>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />

    <GsPropertyModal
      v-model:open="showEdit"
      :property="property"
      :refresh="refresh"
      @delete="onPropertyModalDelete"
    />

    <GsConfirmModal
      v-model:open="showDeleteConfirm"
      :title="t('property.deleteTitle')"
      :description="t('property.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDelete"
    />

    <GsAccommodationModal
      v-model:open="showAccommodationModal"
      :property-id="propertyId"
      :accommodation="editingAccommodation"
      :default-type="defaultAccommodationType"
      :refresh="refreshAccommodations"
      @delete="onAccommodationModalDelete"
    />

    <GsConfirmModal
      v-model:open="showAccommodationDeleteConfirm"
      :title="t('accommodation.deleteTitle')"
      :description="t('accommodation.deleteConfirm')"
      :confirm-label="t('common.delete')"
      danger
      @confirm="confirmDeleteAccommodation"
    />
  </div>
</template>

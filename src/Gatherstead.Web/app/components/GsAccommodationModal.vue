<script setup lang="ts">
import type { AccommodationSummary, AccommodationType, BedSize, BedWriteEntry } from '~/repositories/types'
import { useAccommodationActions } from '~/composables/useAccommodations'
import { effectiveAreaSqMeters, formatArea } from '~/utils/units'

// Create + edit in one component. Passing `accommodation` switches to edit mode; omitting it creates.
const props = defineProps<{
  propertyId: string
  refresh: () => Promise<void>
  accommodation?: AccommodationSummary | null
  defaultType?: AccommodationType
}>()

const emit = defineEmits<{
  delete: [accommodation: AccommodationSummary]
}>()

const open = defineModel<boolean>('open', { default: false })
const { t } = useI18n()

const propertyIdRef = computed(() => props.propertyId)
const { updating, createAccommodation, updateAccommodation } = useAccommodationActions(propertyIdRef, props.refresh)

const isEdit = computed(() => !!props.accommodation)
const saving = computed(() => updating.value.includes(props.accommodation?.id ?? 'new'))

const ALL_TYPES: AccommodationType[] = ['Bedroom', 'Bunk', 'RvPad', 'Tent', 'Offsite']

const typeItems = computed(() =>
  ALL_TYPES.map(type => ({
    label: t(`accommodation.types.${type.charAt(0).toLowerCase() + type.slice(1)}`),
    value: type,
  })),
)

const ALL_BED_SIZES: BedSize[] = ['Single', 'Double', 'Queen', 'King', 'Bunk', 'Sofa', 'Crib', 'Other']
const bedSizeItems = computed(() =>
  ALL_BED_SIZES.map(size => ({ label: t(`accommodation.bedSizes.${size.toLowerCase()}`), value: size })),
)

interface BedRow { size: BedSize, quantity: number }

const form = reactive({
  name: '',
  type: 'Bedroom' as AccommodationType,
  widthMeters: '',
  depthMeters: '',
  areaSqMeters: '',
  beds: [] as BedRow[],
  notes: '',
})
const nameError = ref('')

const numOrNull = (s: string) => s !== '' ? Number(s) : null

const previewArea = computed(() =>
  formatArea(effectiveAreaSqMeters(numOrNull(form.widthMeters), numOrNull(form.depthMeters), numOrNull(form.areaSqMeters))),
)

function addBed() {
  form.beds.push({ size: 'Queen', quantity: 1 })
}
function removeBed(index: number) {
  form.beds.splice(index, 1)
}

watch(open, (isOpen) => {
  if (isOpen) {
    const a = props.accommodation
    form.name = a?.name ?? ''
    form.type = a?.type ?? props.defaultType ?? 'Bedroom'
    form.widthMeters = a?.widthMeters != null ? String(a.widthMeters) : ''
    form.depthMeters = a?.depthMeters != null ? String(a.depthMeters) : ''
    form.areaSqMeters = a?.areaSqMeters != null ? String(a.areaSqMeters) : ''
    form.beds = (a?.beds ?? [])
      .filter((b): b is { id: string, size: BedSize, quantity: number } => b.size != null && b.quantity != null)
      .map(b => ({ size: b.size, quantity: b.quantity }))
    form.notes = a?.notes ?? ''
    nameError.value = ''
  }
})

async function submit() {
  nameError.value = ''
  const trimmed = form.name.trim()
  if (!trimmed) {
    nameError.value = t('validation.required', { field: t('accommodation.name') })
    return
  }
  const dimensions = {
    widthMeters: numOrNull(form.widthMeters),
    depthMeters: numOrNull(form.depthMeters),
    areaSqMeters: numOrNull(form.areaSqMeters),
  }
  const beds: BedWriteEntry[] = form.beds
    .filter(b => b.quantity > 0)
    .map(b => ({ size: b.size, quantity: b.quantity }))
  const notes = form.notes.trim() || null

  const ok = (isEdit.value && props.accommodation)
    ? await updateAccommodation(props.accommodation.id, trimmed, form.type, dimensions, beds, notes)
    : await createAccommodation(trimmed, form.type, dimensions, beds, notes)
  if (ok) open.value = false
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="isEdit ? t('accommodation.editTitle') : t('accommodation.createTitle')"
  >
    <template #body>
      <div class="space-y-5">
        <UFormField :label="t('accommodation.name')" :error="nameError || undefined" required>
          <UInput
            v-model="form.name"
            :placeholder="t('accommodation.name')"
            class="w-full"
            @input="nameError = ''"
          />
        </UFormField>

        <UFormField :label="t('accommodation.type')">
          <USelect v-model="form.type" :items="typeItems" class="w-full" />
        </UFormField>

        <UFormField :label="t('accommodation.beds')">
          <div class="space-y-2">
            <div v-for="(bed, i) in form.beds" :key="i" class="flex items-center gap-2">
              <USelect v-model="bed.size" :items="bedSizeItems" class="flex-1" />
              <UInput v-model.number="bed.quantity" type="number" min="1" class="w-20" />
              <UButton
                color="error"
                variant="ghost"
                icon="i-heroicons-trash"
                :aria-label="t('common.delete')"
                @click="removeBed(i)"
              />
            </div>
            <UButton size="sm" variant="soft" icon="i-heroicons-plus" @click="addBed">
              {{ t('accommodation.addBed') }}
            </UButton>
          </div>
        </UFormField>

        <div class="grid grid-cols-3 gap-3">
          <UFormField :label="t('accommodation.widthLabel')">
            <UInput v-model="form.widthMeters" type="number" min="0" step="0.1" class="w-full" />
          </UFormField>
          <UFormField :label="t('accommodation.depthLabel')">
            <UInput v-model="form.depthMeters" type="number" min="0" step="0.1" class="w-full" />
          </UFormField>
          <UFormField :label="t('accommodation.areaOverrideLabel')">
            <UInput v-model="form.areaSqMeters" type="number" min="0" step="0.1" class="w-full" />
          </UFormField>
        </div>
        <p v-if="previewArea" class="text-xs text-muted -mt-2">
          {{ t('accommodation.areaPreview', { area: previewArea }) }}
        </p>

        <UFormField :label="t('common.notes')">
          <UTextarea v-model="form.notes" class="w-full" />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <GsFormFooter
        :submit-label="isEdit ? t('common.save') : t('common.create')"
        :loading="saving"
        @submit="submit"
        @cancel="open = false"
      >
        <template v-if="isEdit && accommodation" #delete>
          <UButton
            color="error"
            variant="ghost"
            icon="i-heroicons-trash"
            :disabled="saving"
            @click="emit('delete', accommodation!)"
          >
            {{ t('accommodation.deleteTitle') }}
          </UButton>
        </template>
      </GsFormFooter>
    </template>
  </UModal>
</template>

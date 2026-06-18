<script setup lang="ts">
import type { AccommodationSummary, AccommodationType } from '~/repositories/types'
import { useAccommodationActions } from '~/composables/useAccommodations'

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

const form = reactive({
  name: '',
  type: 'Bedroom' as AccommodationType,
  capacityAdults: '',
  capacityChildren: '',
  notes: '',
})
const nameError = ref('')

watch(open, (isOpen) => {
  if (isOpen) {
    form.name = props.accommodation?.name ?? ''
    form.type = props.accommodation?.type ?? props.defaultType ?? 'Bedroom'
    form.capacityAdults = props.accommodation?.capacityAdults != null ? String(props.accommodation.capacityAdults) : ''
    form.capacityChildren = props.accommodation?.capacityChildren != null ? String(props.accommodation.capacityChildren) : ''
    form.notes = props.accommodation?.notes ?? ''
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
  const capacityAdults = form.capacityAdults !== '' ? Number(form.capacityAdults) : null
  const capacityChildren = form.capacityChildren !== '' ? Number(form.capacityChildren) : null
  const notes = form.notes.trim() || null

  const ok = (isEdit.value && props.accommodation)
    ? await updateAccommodation(props.accommodation.id, trimmed, form.type, capacityAdults, capacityChildren, notes)
    : await createAccommodation(trimmed, form.type, capacityAdults, capacityChildren, notes)
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

        <div class="grid grid-cols-2 gap-4">
          <UFormField :label="t('accommodation.capacityAdultsLabel')">
            <UInput v-model="form.capacityAdults" type="number" min="0" class="w-full" />
          </UFormField>
          <UFormField :label="t('accommodation.capacityChildrenLabel')">
            <UInput v-model="form.capacityChildren" type="number" min="0" class="w-full" />
          </UFormField>
        </div>

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

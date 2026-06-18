<script setup lang="ts">
import type { AgeBand, AttributeWriteEntry } from '~/repositories/types'
import { useDietaryTags } from '~/composables/useDietaryTags'
import { useAgeBands } from '~/composables/useAgeBands'

const name = defineModel<string>('name', { required: true })
const isAdult = defineModel<boolean>('isAdult', { required: true })
const ageBand = defineModel<string>('ageBand', { required: true })
const birthDate = defineModel<string>('birthDate', { required: true })
const dietaryNotes = defineModel<string>('dietaryNotes', { required: true })
const dietaryTags = defineModel<string[]>('dietaryTags', { required: true })
const attributes = defineModel<AttributeWriteEntry[]>('attributes', { required: true })

const props = defineProps<{
  nameError: string
  loading: boolean
  cancelTo: string
  submitLabel: string
}>()

const emit = defineEmits<{
  submit: []
  clearNameError: []
}>()

const { t } = useI18n()
const { tagsByCategory } = useDietaryTags()
const { selectItems: ageBandItems, deriveFromBirthDate } = useAgeBands()

const CATEGORY_ICON: Record<'Allergy' | 'Restriction' | 'Diet', string> = {
  Allergy: 'i-heroicons-exclamation-triangle',
  Restriction: 'i-heroicons-no-symbol',
  Diet: 'i-heroicons-check-circle',
}

const tagItems = computed(() => {
  const cats: ('Allergy' | 'Restriction' | 'Diet')[] = ['Allergy', 'Restriction', 'Diet']
  return cats.flatMap(key =>
    tagsByCategory.value[key].map(tag => ({
      label: tag.displayName,
      value: tag.slug,
      icon: CATEGORY_ICON[key],
    })),
  )
})

const derivedBand = computed(() => birthDate.value ? deriveFromBirthDate(birthDate.value) : null)

const displayedAgeBand = computed({
  get: () => derivedBand.value ?? ((ageBand.value as AgeBand | '') || undefined),
  set: (val) => { ageBand.value = val ?? '' },
})

watch(birthDate, (bd) => {
  if (bd) ageBand.value = ''
})
</script>

<template>
  <UForm
    :state="{ name, isAdult, ageBand, birthDate, dietaryNotes, dietaryTags }"
    class="max-w-lg space-y-5"
    @submit="emit('submit')"
  >
    <UFormField :label="t('member.name')" name="name" :error="props.nameError || undefined" required>
      <UInput
        v-model="name"
        :placeholder="t('member.name')"
        required
        class="w-full"
        @input="emit('clearNameError')"
      />
    </UFormField>

    <UFormField :label="t('member.isAdult')" name="isAdult">
      <UCheckbox v-model="isAdult" :label="t('member.adult')" />
    </UFormField>

    <UFormField :label="t('member.birthDate')" name="birthDate">
      <UInput v-model="birthDate" type="date" class="w-full" />
    </UFormField>

    <UFormField :label="t('member.ageBand')" name="ageBand" :hint="derivedBand ? t('member.ageBandAuto') : undefined">
      <USelect
        v-model="displayedAgeBand"
        :items="ageBandItems"
        :disabled="!!derivedBand"
        :placeholder="t('member.ageBandPlaceholder')"
        class="w-full"
      />
    </UFormField>

    <UFormField :label="t('member.dietaryNotes')" name="dietaryNotes">
      <UTextarea v-model="dietaryNotes" :placeholder="t('member.dietaryNotesPlaceholder')" class="w-full" />
    </UFormField>

    <UFormField :label="t('member.dietaryTags')" name="dietaryTags">
      <USelectMenu
        v-model="dietaryTags"
        :items="tagItems"
        value-key="value"
        :placeholder="t('member.dietaryTagsPlaceholder')"
        :content="{ side: 'bottom' }"
        multiple
        clear
        class="w-full"
      />
      <div v-if="dietaryTags.length" class="mt-2">
        <GsDietaryTags :slugs="dietaryTags" />
      </div>
    </UFormField>

    <GsAttributeField v-model="attributes" />

    <GsFormFooter
      submit-type="submit"
      :submit-label="props.submitLabel"
      :loading="props.loading"
      :cancel-to="props.cancelTo"
    >
      <template v-if="$slots.delete" #delete>
        <slot name="delete" />
      </template>
    </GsFormFooter>
  </UForm>
</template>

<script setup lang="ts">
const name = defineModel<string>('name', { required: true })
const isAdult = defineModel<boolean>('isAdult', { required: true })
const ageBand = defineModel<string>('ageBand', { required: true })
const birthDate = defineModel<string>('birthDate', { required: true })
const dietaryNotes = defineModel<string>('dietaryNotes', { required: true })
const dietaryTagsInput = defineModel<string>('dietaryTagsInput', { required: true })

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
</script>

<template>
  <UForm
    :state="{ name, isAdult, ageBand, birthDate, dietaryNotes, dietaryTagsInput }"
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

    <UFormField :label="t('member.ageBand')" name="ageBand">
      <UInput v-model="ageBand" :placeholder="t('member.ageBandPlaceholder')" class="w-full" />
    </UFormField>

    <UFormField :label="t('member.birthDate')" name="birthDate">
      <UInput v-model="birthDate" type="date" class="w-full" />
    </UFormField>

    <UFormField :label="t('member.dietaryNotes')" name="dietaryNotes">
      <UTextarea v-model="dietaryNotes" :placeholder="t('member.dietaryNotesPlaceholder')" class="w-full" />
    </UFormField>

    <UFormField :label="t('member.dietaryTags')" name="dietaryTags" :hint="t('member.dietaryTagsHint')">
      <UInput v-model="dietaryTagsInput" :placeholder="t('member.dietaryTagsPlaceholder')" class="w-full" />
    </UFormField>

    <div class="flex items-center gap-3 pt-2">
      <UButton type="submit" :loading="props.loading">
        {{ props.submitLabel }}
      </UButton>
      <UButton variant="ghost" :to="props.cancelTo">
        {{ t('common.cancel') }}
      </UButton>
    </div>
  </UForm>
</template>

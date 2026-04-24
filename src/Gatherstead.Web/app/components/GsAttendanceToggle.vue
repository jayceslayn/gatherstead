<script setup lang="ts">
import type { AttendanceStatus } from '~/composables/useEventAttendance'

const props = defineProps<{
  modelValue: AttendanceStatus | null
  loading?: boolean
  size?: 'xs' | 'sm' | 'md'
}>()

const emit = defineEmits<{
  'update:modelValue': [value: AttendanceStatus]
}>()

const { t } = useI18n()

const pendingValue = ref<AttendanceStatus | null>(null)

watch(() => props.loading, (val) => {
  if (!val) pendingValue.value = null
})

function select(value: AttendanceStatus) {
  pendingValue.value = value
  emit('update:modelValue', value)
}

const options: Array<{
  value: AttendanceStatus
  icon: string
  labelKey: string
  activeColor: 'success' | 'secondary' | 'neutral'
}> = [
  { value: 'Going', icon: 'i-heroicons-check', labelKey: 'status.going', activeColor: 'success' },
  { value: 'Maybe', icon: 'i-heroicons-question-mark-circle', labelKey: 'status.maybe', activeColor: 'neutral' },
  { value: 'NotGoing', icon: 'i-heroicons-x-mark', labelKey: 'status.notGoing', activeColor: 'secondary' },
]
</script>

<template>
  <div
    class="flex items-center gap-1"
    role="group"
    :aria-label="t('event.myAttendance')"
  >
    <UButton
      v-for="opt in options"
      :key="opt.value"
      :color="modelValue === opt.value ? opt.activeColor : 'neutral'"
      :variant="modelValue === opt.value ? 'solid' : 'ghost'"
      :size="size ?? 'sm'"
      :icon="opt.icon"
      :loading="loading && pendingValue === opt.value"
      :disabled="loading"
      :aria-label="t(opt.labelKey)"
      :aria-pressed="modelValue === opt.value"
      @click="select(opt.value)"
    />
  </div>
</template>

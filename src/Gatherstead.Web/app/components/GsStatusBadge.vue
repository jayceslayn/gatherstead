<script setup lang="ts">
type StatusValue =
  | 'Covered' | 'Partial' | 'Open' | 'Exception'
  | 'Going' | 'Maybe' | 'NotGoing' | 'NoResponse'
  | 'Intent' | 'Hold' | 'Confirmed' | 'Declined'

const props = defineProps<{
  status: StatusValue
  iconOnly?: boolean
}>()

const { t } = useI18n()

interface StatusConfig {
  color: 'success' | 'secondary' | 'neutral' | 'primary' | 'error'
  icon: string
  labelKey: string
}

const statusMap: Record<StatusValue, StatusConfig> = {
  Covered: { color: 'success', icon: 'i-heroicons-check-circle', labelKey: 'status.covered' },
  Partial: { color: 'secondary', icon: 'i-heroicons-clock', labelKey: 'status.partial' },
  Open: { color: 'neutral', icon: 'i-heroicons-question-mark-circle', labelKey: 'status.open' },
  Exception: { color: 'primary', icon: 'i-heroicons-x-circle', labelKey: 'status.exception' },
  Going: { color: 'success', icon: 'i-heroicons-check-circle', labelKey: 'status.going' },
  Maybe: { color: 'secondary', icon: 'i-heroicons-question-mark-circle', labelKey: 'status.maybe' },
  NotGoing: { color: 'neutral', icon: 'i-heroicons-x-mark', labelKey: 'status.notGoing' },
  NoResponse: { color: 'neutral', icon: 'i-heroicons-minus', labelKey: 'status.noResponse' },
  Intent: { color: 'neutral', icon: 'i-heroicons-hand-raised', labelKey: 'status.intent' },
  Hold: { color: 'secondary', icon: 'i-heroicons-pause-circle', labelKey: 'status.hold' },
  Confirmed: { color: 'success', icon: 'i-heroicons-lock-closed', labelKey: 'status.confirmed' },
  Declined: { color: 'error', icon: 'i-heroicons-x-circle', labelKey: 'status.declined' },
}

const config = computed(() => statusMap[props.status])
</script>

<template>
  <UBadge
    v-if="config"
    :color="config.color"
    variant="subtle"
    :icon="config.icon"
    :aria-label="t(config.labelKey)"
  >
    <span v-if="!iconOnly">{{ t(config.labelKey) }}</span>
  </UBadge>
</template>

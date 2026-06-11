<script setup lang="ts">
import { taskCoverageColor } from '~/composables/useReportView'

// Assignee/volunteer coverage badge shared by the event report and the sign-up
// page. Colour reflects minimum-assignee coverage: green (met), amber (partial),
// red (none yet), neutral when no minimum is configured.
const props = defineProps<{
  assigneeCount: number
  minimumAssignees?: number | null
}>()

const { t } = useI18n()
</script>

<template>
  <UBadge
    :color="taskCoverageColor(assigneeCount, props.minimumAssignees)"
    variant="subtle"
    icon="i-heroicons-user-group"
  >
    {{ props.minimumAssignees != null
      ? t('report.event.assigneeRatio', { n: assigneeCount, m: props.minimumAssignees })
      : t('report.event.assigneeCount', { n: assigneeCount }) }}
  </UBadge>
</template>

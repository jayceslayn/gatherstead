<script setup lang="ts">
import type { EventReportTask } from '~/repositories/types'

// One task plan on one day. Collapsed shows the headline coverage badge;
// expanding the lane (or printing) reveals assignee names and any exception
// reason. In the swimlane grid the lane rule carries the task name; the print
// day-stack has no rule, so it opts into `showTitle`.
const props = defineProps<{
  task: EventReportTask
  expanded?: boolean
  showTitle?: boolean
}>()

const { t } = useI18n()

// Detail stays in the DOM (hidden) so the print variant can reveal it without juggling state.
const detailClass = computed(() =>
  props.expanded ? 'space-y-2' : 'hidden print:block print:space-y-2')
</script>

<template>
  <UCard :ui="{ body: 'p-3 sm:p-3' }" class="print:break-inside-avoid">
    <div class="text-sm space-y-2">
      <div class="flex items-start justify-between gap-3">
        <div v-if="showTitle" class="flex items-center gap-2 flex-wrap min-w-0">
          <p class="font-semibold">{{ task.templateName }}</p>
          <span v-if="task.timeSlot" class="text-xs text-muted">{{ t(`event.task.${task.timeSlot.toLowerCase()}`) }}</span>
        </div>
        <GsTaskCoverageBadge
          :assignee-count="task.assigneeCount"
          :minimum-assignees="task.minimumAssignees"
          class="shrink-0 ml-auto"
        />
      </div>

      <div :class="detailClass">
        <p v-if="task.isException && task.exceptionReason" class="text-muted italic">
          {{ task.exceptionReason }}
        </p>

        <ul v-if="task.assignees.length" class="space-y-0.5">
          <li v-for="(name, i) in task.assignees" :key="i" class="truncate">{{ name }}</li>
        </ul>
        <p v-else class="text-xs text-muted">{{ t('report.event.noAssignees') }}</p>
      </div>
    </div>
  </UCard>
</template>

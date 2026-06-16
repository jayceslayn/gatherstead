<script setup lang="ts">
import type { EventReportTask } from '~/repositories/types'

const props = defineProps<{
  task: EventReportTask
  expanded: Set<string>
}>()

const emit = defineEmits<{ toggle: [id: string] }>()

const { t } = useI18n()

function isExpanded(id: string) {
  return props.expanded.has(id)
}
// Detail stays in the DOM (hidden) so the print variant can reveal it without juggling state.
function detailClass(id: string, expandedClasses: string) {
  return isExpanded(id) ? expandedClasses : 'hidden print:block print:mt-3 print:space-y-2'
}
</script>

<template>
  <UCard :ui="{ body: 'p-3 sm:p-3' }" class="print:break-inside-avoid">
    <button
      type="button"
      class="w-full flex items-start justify-between gap-3 text-left"
      :aria-expanded="isExpanded(task.taskPlanId)"
      :aria-label="isExpanded(task.taskPlanId) ? t('report.event.hideDetails') : t('report.event.showDetails')"
      @click="emit('toggle', task.taskPlanId)"
    >
      <div class="min-w-0">
        <div class="flex items-center gap-2 flex-wrap">
          <p class="font-semibold">{{ task.templateName }}</p>
          <span v-if="task.timeSlot" class="text-xs text-muted">{{ t(`event.task.${task.timeSlot.toLowerCase()}`) }}</span>
        </div>
        <div class="flex flex-wrap items-center gap-1.5 mt-2">
          <GsTaskCoverageBadge :assignee-count="task.assigneeCount" :minimum-assignees="task.minimumAssignees" />
        </div>
      </div>
      <UIcon
        name="i-heroicons-chevron-down"
        class="size-5 shrink-0 mt-1 transition-transform print:hidden"
        :class="isExpanded(task.taskPlanId) ? 'rotate-180' : ''"
      />
    </button>

    <div :class="['text-sm', detailClass(task.taskPlanId, 'mt-3 space-y-2')]">
      <p v-if="task.isException && task.exceptionReason" class="text-muted italic">
        {{ task.exceptionReason }}
      </p>
      <div v-if="task.assignees.length">
        <p class="text-muted text-xs uppercase tracking-wide mb-1.5">{{ t('report.event.assignees') }}</p>
        <ul class="space-y-0.5">
          <li v-for="(name, i) in task.assignees" :key="i">{{ name }}</li>
        </ul>
      </div>
      <p v-else class="text-muted">{{ t('report.event.noAssignees') }}</p>
    </div>
  </UCard>
</template>

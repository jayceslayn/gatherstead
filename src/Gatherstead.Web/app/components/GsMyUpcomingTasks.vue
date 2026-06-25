<script setup lang="ts">
import { useMyTasks } from '~/composables/useMyUpcoming'
import type { MyTask } from '~/repositories/types'

const props = withDefaults(defineProps<{ limit?: number }>(), { limit: 5 })

const { t } = useI18n()
const { formatDate } = useFormatDate()
const { tasks, pending } = useMyTasks()

const visible = computed(() => tasks.value.slice(0, props.limit))

function taskMeta(task: MyTask): string {
  const parts = [task.eventName, formatDate(task.day)]
  if (task.timeSlot) parts.push(t(`event.task.${task.timeSlot.toLowerCase()}`))
  return parts.join(' · ')
}
</script>

<template>
  <div>
    <h2 class="text-xs font-semibold text-muted uppercase tracking-wider mb-3">
      {{ t('dashboard.myTasks') }}
    </h2>

    <div v-if="pending" class="rounded-lg border border-(--ui-border) bg-elevated p-6 text-center">
      <p class="text-sm text-muted">{{ t('common.loading') }}</p>
    </div>

    <div
      v-else-if="!visible.length"
      class="rounded-lg border border-(--ui-border) bg-elevated p-6 flex flex-col items-center text-center gap-2"
    >
      <UIcon name="i-heroicons-clipboard-document-list" class="size-8 text-muted" />
      <p class="text-sm text-muted">{{ t('dashboard.noTasks') }}</p>
    </div>

    <ul v-else class="space-y-2">
      <li v-for="task in visible" :key="task.id">
        <NuxtLink
          :to="`/app/events/${task.eventId}#tasks`"
          class="rounded-lg border border-(--ui-border) bg-elevated p-3 flex items-center gap-3 hover:ring-1 hover:ring-primary transition-all"
        >
          <UIcon
            :name="task.completed ? 'i-heroicons-check-circle' : 'i-heroicons-clipboard-document-list'"
            class="size-5 shrink-0"
            :class="task.completed ? 'text-success' : 'text-primary'"
          />
          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium truncate">{{ task.taskName }}</p>
            <p class="text-xs text-muted truncate">{{ taskMeta(task) }}</p>
          </div>
          <UIcon name="i-heroicons-chevron-right" class="size-4 text-muted shrink-0" />
        </NuxtLink>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { TableColumn, DropdownMenuItem } from '@nuxt/ui'
import type { AttendanceStatus, HouseholdMember } from '~/repositories/types'

export interface AttendanceGridColumn {
  id: string
  label: string
  sublabel?: string
}

const props = defineProps<{
  members: HouseholdMember[]
  columns: AttendanceGridColumn[]
  statusByMemberColumn: Record<string, Record<string, AttendanceStatus | undefined>>
  totals: Record<string, { going: number, maybe: number }>
  updating: Record<string, boolean>
  loaded: boolean
}>()

const emit = defineEmits<{
  'set-cell': [memberId: string, columnId: string, status: AttendanceStatus]
  'set-row': [memberId: string, status: AttendanceStatus]
  'set-column': [columnId: string, status: AttendanceStatus]
}>()

const { t } = useI18n()

const MEMBER_COLUMN_ID = 'member'

const tableColumns = computed<TableColumn<HouseholdMember>[]>(() => [
  { id: MEMBER_COLUMN_ID, header: '', footer: '' },
  ...props.columns.map(col => ({ id: col.id, header: '', footer: '' } as TableColumn<HouseholdMember>)),
])

function cellKey(memberId: string, columnId: string) {
  return `${memberId}:${columnId}`
}

function bulkItems(onSelect: (status: AttendanceStatus) => void): DropdownMenuItem[][] {
  return [[
    { label: t('status.going'), icon: 'i-heroicons-check', onSelect: () => onSelect('Going') },
    { label: t('status.maybe'), icon: 'i-heroicons-question-mark-circle', onSelect: () => onSelect('Maybe') },
    { label: t('status.notGoing'), icon: 'i-heroicons-x-mark', onSelect: () => onSelect('NotGoing') },
  ]]
}
</script>

<template>
  <div class="max-w-3xl">
    <div v-if="!loaded" class="py-6 text-center text-sm text-muted">
      {{ t('common.loading') }}
    </div>

    <div v-else-if="!members.length" class="py-6 text-center text-sm text-muted">
      {{ t('member.noMembers') }}
    </div>

    <template v-else>
      <p class="mb-2 text-xs text-muted flex items-center gap-1.5">
        <UIcon name="i-heroicons-information-circle" class="size-3.5 shrink-0" />
        {{ t('event.attendanceGrid.hint') }}
      </p>

      <UTable
        :data="members"
        :columns="tableColumns"
        :get-row-id="(row) => row.id"
        :initial-state="{ columnPinning: { left: [MEMBER_COLUMN_ID] } }"
        sticky
        class="max-h-[30rem] rounded-lg border border-(--ui-border)"
        :ui="{
          base: 'border-separate border-spacing-0',
          th: 'whitespace-nowrap',
          td: 'whitespace-nowrap align-middle',
        }"
      >
        <template #[`${MEMBER_COLUMN_ID}-header`]>
          <span class="font-semibold text-muted">{{ t('member.title') }}</span>
        </template>

        <template #[`${MEMBER_COLUMN_ID}-cell`]="{ row }">
          <div class="flex items-center justify-between gap-2 min-w-36">
            <span class="font-medium text-default">{{ row.original.name }}</span>
            <UDropdownMenu :items="bulkItems((status) => emit('set-row', row.original.id, status))">
              <UButton
                size="xs"
                variant="ghost"
                color="neutral"
                icon="i-heroicons-ellipsis-vertical"
                :aria-label="t('event.attendanceGrid.rowActions', { name: row.original.name })"
              />
            </UDropdownMenu>
          </div>
        </template>

        <template #[`${MEMBER_COLUMN_ID}-footer`]>
          <span class="font-semibold text-muted">{{ t('event.attendanceGrid.totals') }}</span>
        </template>

        <template
          v-for="col in columns"
          :key="`header-${col.id}`"
          #[`${col.id}-header`]
        >
          <div class="flex items-center justify-center gap-1 w-32 min-w-32">
            <div class="text-center leading-tight">
              <div class="text-muted">{{ col.label }}</div>
              <div v-if="col.sublabel" class="text-xs text-muted/70">{{ col.sublabel }}</div>
            </div>
            <UDropdownMenu :items="bulkItems((status) => emit('set-column', col.id, status))">
              <UButton
                size="xs"
                variant="ghost"
                color="neutral"
                icon="i-heroicons-ellipsis-vertical"
                :aria-label="t('event.attendanceGrid.columnActions', { day: col.sublabel ? `${col.label} – ${col.sublabel}` : col.label })"
              />
            </UDropdownMenu>
          </div>
        </template>

        <template
          v-for="col in columns"
          :key="`cell-${col.id}`"
          #[`${col.id}-cell`]="{ row }"
        >
          <div class="flex justify-center w-32 min-w-32">
            <GsAttendanceToggle
              :model-value="statusByMemberColumn[row.original.id]?.[col.id] ?? null"
              :loading="updating[cellKey(row.original.id, col.id)]"
              size="xs"
              @update:model-value="emit('set-cell', row.original.id, col.id, $event)"
            />
          </div>
        </template>

        <template
          v-for="col in columns"
          :key="`footer-${col.id}`"
          #[`${col.id}-footer`]
        >
          <div class="flex items-center justify-center gap-3 text-xs text-muted w-32 min-w-32">
            <span class="inline-flex items-center gap-1">
              <UIcon name="i-heroicons-check" class="size-3.5 text-success" />
              <span class="tabular-nums">{{ totals[col.id]?.going ?? 0 }}</span>
            </span>
            <span class="inline-flex items-center gap-1">
              <UIcon name="i-heroicons-question-mark-circle" class="size-3.5" />
              <span class="tabular-nums">{{ totals[col.id]?.maybe ?? 0 }}</span>
            </span>
          </div>
        </template>
      </UTable>
    </template>
  </div>
</template>

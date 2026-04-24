<script setup lang="ts">
import type { TableColumn, DropdownMenuItem } from '@nuxt/ui'
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useEventAttendance } from '~/composables/useEventAttendance'
import type { AttendanceStatus, HouseholdMember } from '~/repositories/types'

const props = defineProps<{
  eventId: string
  days: string[]
  householdId: string
}>()

const { t } = useI18n()
const { attendance, upsert } = useEventAttendance(computed(() => props.eventId))
const { members } = useHouseholdMembers(computed(() => props.householdId))

const statusByMemberDay = computed<Record<string, Record<string, AttendanceStatus | undefined>>>(() => {
  const memberIds = new Set(members.value.map(m => m.id))
  const result: Record<string, Record<string, AttendanceStatus | undefined>> = {}
  for (const m of members.value) {
    result[m.id] = {}
  }
  for (const a of attendance.value) {
    if (memberIds.has(a.householdMemberId)) {
      result[a.householdMemberId]![a.day] = a.status
    }
  }
  return result
})

const updating = ref<Record<string, boolean>>({})

function cellKey(memberId: string, day: string) {
  return `${memberId}:${day}`
}

async function setCell(memberId: string, day: string, status: AttendanceStatus) {
  if (!props.householdId) return
  const key = cellKey(memberId, day)
  updating.value[key] = true
  try {
    await upsert(props.householdId, memberId, day, status)
  }
  finally {
    updating.value[key] = false
  }
}

async function setRow(memberId: string, status: AttendanceStatus) {
  await Promise.all(props.days.map(day => setCell(memberId, day, status)))
}

async function setColumn(day: string, status: AttendanceStatus) {
  await Promise.all(members.value.map(m => setCell(m.id, day, status)))
}

const eventDayTotals = computed<Record<string, { going: number, maybe: number }>>(() => {
  const totals: Record<string, { going: number, maybe: number }> = {}
  for (const day of props.days) totals[day] = { going: 0, maybe: 0 }
  for (const a of attendance.value) {
    const entry = totals[a.day]
    if (!entry) continue
    if (a.status === 'Going') entry.going++
    else if (a.status === 'Maybe') entry.maybe++
  }
  return totals
})

function formatDayLabel(date: string) {
  return new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
    new Date(date + 'T00:00:00'),
  )
}

const MEMBER_COLUMN_ID = 'member'

const columns = computed<TableColumn<HouseholdMember>[]>(() => [
  { id: MEMBER_COLUMN_ID, header: '', footer: '' },
  ...props.days.map(day => ({ id: day, header: '', footer: '' } as TableColumn<HouseholdMember>)),
])

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
    <div v-if="!householdId" class="py-6 text-center text-sm text-muted">
      {{ t('common.loading') }}
    </div>

    <div v-else-if="!members.length" class="py-6 text-center text-sm text-muted">
      {{ t('member.noMembers') }}
    </div>

    <UTable
      v-else
      :data="members"
      :columns="columns"
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
          <UDropdownMenu :items="bulkItems((status) => setRow(row.original.id, status))">
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
        v-for="day in days"
        :key="`header-${day}`"
        #[`${day}-header`]
      >
        <div class="flex items-center justify-center gap-1 w-32 min-w-32">
          <span class="text-muted">{{ formatDayLabel(day) }}</span>
          <UDropdownMenu :items="bulkItems((status) => setColumn(day, status))">
            <UButton
              size="xs"
              variant="ghost"
              color="neutral"
              icon="i-heroicons-ellipsis-vertical"
              :aria-label="t('event.attendanceGrid.columnActions', { day: formatDayLabel(day) })"
            />
          </UDropdownMenu>
        </div>
      </template>

      <template
        v-for="day in days"
        :key="`cell-${day}`"
        #[`${day}-cell`]="{ row }"
      >
        <div class="flex justify-center w-32 min-w-32">
          <GsAttendanceToggle
            :model-value="statusByMemberDay[row.original.id]?.[day] ?? null"
            :loading="updating[cellKey(row.original.id, day)]"
            size="xs"
            @update:model-value="setCell(row.original.id, day, $event)"
          />
        </div>
      </template>

      <template
        v-for="day in days"
        :key="`footer-${day}`"
        #[`${day}-footer`]
      >
        <div class="flex items-center justify-center gap-3 text-xs text-muted w-32 min-w-32">
          <span class="inline-flex items-center gap-1">
            <UIcon name="i-heroicons-check" class="size-3.5 text-success" />
            <span class="tabular-nums">{{ eventDayTotals[day]?.going ?? 0 }}</span>
          </span>
          <span class="inline-flex items-center gap-1">
            <UIcon name="i-heroicons-question-mark-circle" class="size-3.5" />
            <span class="tabular-nums">{{ eventDayTotals[day]?.maybe ?? 0 }}</span>
          </span>
        </div>
      </template>
    </UTable>
  </div>
</template>

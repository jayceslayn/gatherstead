<script setup lang="ts">
import { useHouseholdMembers } from '~/composables/useHouseholdMembers'
import { useEventAttendance } from '~/composables/useEventAttendance'
import type { AttendanceStatus } from '~/repositories/types'

const props = defineProps<{
  eventId: string
  days: string[]
  householdId: string
}>()

const { attendance, upsert } = useEventAttendance(computed(() => props.eventId))
const { members } = useHouseholdMembers(computed(() => props.householdId))

const columns = computed(() =>
  props.days.map(day => ({
    id: day,
    label: new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric' }).format(
      new Date(day + 'T00:00:00'),
    ),
  })),
)

const statusByMemberColumn = computed<Record<string, Record<string, AttendanceStatus | undefined>>>(() => {
  const memberIds = new Set(members.value.map(m => m.id))
  const result: Record<string, Record<string, AttendanceStatus | undefined>> = {}
  for (const m of members.value) result[m.id] = {}
  for (const a of attendance.value) {
    if (memberIds.has(a.householdMemberId)) {
      result[a.householdMemberId]![a.day] = a.status
    }
  }
  return result
})

const totals = computed<Record<string, { going: number, maybe: number }>>(() => {
  const result: Record<string, { going: number, maybe: number }> = {}
  for (const day of props.days) result[day] = { going: 0, maybe: 0 }
  for (const a of attendance.value) {
    const entry = result[a.day]
    if (!entry) continue
    if (a.status === 'Going') entry.going++
    else if (a.status === 'Maybe') entry.maybe++
  }
  return result
})

const updating = ref<Record<string, boolean>>({})

function cellKey(memberId: string, columnId: string) {
  return `${memberId}:${columnId}`
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

async function setColumn(columnId: string, status: AttendanceStatus) {
  await Promise.all(members.value.map(m => setCell(m.id, columnId, status)))
}
</script>

<template>
  <GsAttendanceGrid
    :members="members"
    :columns="columns"
    :status-by-member-column="statusByMemberColumn"
    :totals="totals"
    :updating="updating"
    :loaded="!!householdId"
    @set-cell="setCell"
    @set-row="setRow"
    @set-column="setColumn"
  />
</template>

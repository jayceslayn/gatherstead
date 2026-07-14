<script setup lang="ts">
import type { MealPlan, TaskPlan } from '~/repositories/types'
import { useTenantStore } from '~/stores/tenant'
import { useMealPlanActions } from '~/composables/useMealPlans'
import { useTaskPlanActions } from '~/composables/useTaskTemplates'
import { useTemplateFormatting } from '~/composables/useTemplateFormatting'
import { mealSlotRank, taskSlotRank } from '~/composables/useTemplateOrder'

const props = defineProps<{
  eventId: string
  templateId: string
  kind: 'meal' | 'task'
}>()

const { t } = useI18n()
const { formatDay } = useFormatDate()
const tenantStore = useTenantStore()
const { mealPlans: mealRepo, tasks: taskRepo } = useRepositories()

const eventIdRef = computed(() => props.eventId)
const templateIdRef = computed(() => props.templateId)
const labelKey = computed(() => (props.kind === 'meal' ? 'event.meal.plan' : 'event.task.plan'))

// Plans are only fetched once the section is expanded, so opening the edit tab
// doesn't fire a listPlans call for every template up front.
const expanded = ref(false)
const { data, pending, refresh } = useAsyncData<(MealPlan | TaskPlan)[]>(
  () => `manage-plans-${props.kind}-${tenantStore.currentTenantId}-${props.templateId}`,
  () => (props.kind === 'meal'
    ? mealRepo.listPlans(tenantStore.currentTenantId!, props.eventId, props.templateId)
    : taskRepo.listPlans(tenantStore.currentTenantId!, props.eventId, props.templateId)),
  { immediate: false, watch: [templateIdRef] },
)

function toggle() {
  expanded.value = !expanded.value
  if (expanded.value && !data.value) refresh()
}

const mealActions = useMealPlanActions(eventIdRef, templateIdRef, refresh)
const taskActions = useTaskPlanActions(eventIdRef, templateIdRef, refresh)
const updating = computed(() => (props.kind === 'meal' ? mealActions.updating.value : taskActions.updating.value))

interface PlanRow {
  id: string
  day: string
  order: number
  label: string
  notes: string | null
}

const { mealTypeLabel, taskSlotLabel } = useTemplateFormatting()

function slotLabel(p: MealPlan | TaskPlan): string {
  if (props.kind === 'meal') return mealTypeLabel((p as MealPlan).mealType)
  const slot = (p as TaskPlan).timeSlot
  return slot ? taskSlotLabel(slot) : ''
}

// Canonical slot ranking from useTemplateOrder (Anytime last), so this list orders
// the same way as the sign-up grids and the report.
function slotOrder(p: MealPlan | TaskPlan): number {
  if (props.kind === 'meal') return mealSlotRank((p as MealPlan).mealType)
  return taskSlotRank((p as TaskPlan).timeSlot)
}

const rows = computed<PlanRow[]>(() =>
  [...(data.value ?? [])]
    .map(p => ({ id: p.id, day: p.day, order: slotOrder(p), label: slotLabel(p), notes: p.notes }))
    .sort((a, b) => a.day.localeCompare(b.day) || a.order - b.order),
)

// Edit modal state — only the note is editable; other plan fields are preserved.
const editOpen = ref(false)
const editNotes = ref('')
const editing = ref<MealPlan | TaskPlan | null>(null)
const editLoading = computed(() => (editing.value ? updating.value.includes(editing.value.id) : false))

function openEdit(id: string) {
  const plan = (data.value ?? []).find(p => p.id === id) ?? null
  editing.value = plan
  editNotes.value = plan?.notes ?? ''
  editOpen.value = true
}

async function submitEdit() {
  const plan = editing.value
  if (!plan) return
  const notes = editNotes.value.trim() || null
  const ok = props.kind === 'meal'
    ? await mealActions.updatePlan(plan.id, notes, (plan as MealPlan).isException, (plan as MealPlan).exceptionReason)
    : await taskActions.updatePlan(plan.id, (plan as TaskPlan).completed, notes, (plan as TaskPlan).isException, (plan as TaskPlan).exceptionReason)
  if (ok) editOpen.value = false
}

// Delete state
const toDelete = ref<string | null>(null)
async function confirmDelete() {
  const id = toDelete.value
  toDelete.value = null
  if (!id) return
  if (props.kind === 'meal') await mealActions.deletePlan(id)
  else await taskActions.deletePlan(id)
}
</script>

<template>
  <div class="mt-3 pt-3 border-t border-default">
    <UButton
      variant="link"
      size="xs"
      color="neutral"
      :icon="expanded ? 'i-heroicons-chevron-down' : 'i-heroicons-chevron-right'"
      class="px-0"
      @click="toggle"
    >
      {{ t(`${labelKey}.manage`) }}
    </UButton>

    <div v-if="expanded" class="mt-2">
      <div v-if="pending" class="py-2 text-xs text-muted">{{ t('common.loading') }}</div>
      <p v-else-if="!rows.length" class="py-2 text-xs text-muted">{{ t(`${labelKey}.empty`) }}</p>
      <ul v-else class="divide-y divide-default">
        <li v-for="row in rows" :key="row.id" class="flex items-center gap-3 py-2">
          <div class="min-w-0 flex-1">
            <p class="text-sm font-medium">
              {{ formatDay(row.day) }}
              <span class="text-muted font-normal before:content-['·'] before:mx-1">{{ row.label }}</span>
            </p>
            <p v-if="row.notes" class="text-xs text-muted truncate">{{ row.notes }}</p>
          </div>
          <GsRoleGate min-role="Coordinator">
            <div class="flex items-center shrink-0">
              <UButton
                variant="ghost"
                size="xs"
                icon="i-heroicons-pencil"
                :aria-label="t('common.edit')"
                @click="openEdit(row.id)"
              />
              <UButton
                color="error"
                variant="ghost"
                size="xs"
                icon="i-heroicons-trash"
                :aria-label="t('common.delete')"
                :loading="updating.includes(row.id)"
                @click="() => { toDelete = row.id }"
              />
            </div>
          </GsRoleGate>
        </li>
      </ul>
    </div>

    <GsPlanEditModal
      v-model:open="editOpen"
      v-model:notes="editNotes"
      :title="t(`${labelKey}.editTitle`)"
      :loading="editLoading"
      @submit="submitEdit"
    />
    <GsConfirmModal
      :open="!!toDelete"
      :title="t(`${labelKey}.deleteTitle`)"
      :description="t(`${labelKey}.deleteConfirm`)"
      :confirm-label="t('common.delete')"
      danger
      @update:open="(val: boolean) => { if (!val) toDelete = null }"
      @confirm="confirmDelete"
    />
  </div>
</template>

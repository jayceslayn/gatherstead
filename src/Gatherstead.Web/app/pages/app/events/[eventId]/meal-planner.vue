<script setup lang="ts">
import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import { useEvent } from '~/composables/useEvents'
import { useEventReport } from '~/composables/useEventReport'
import { useTenantRole } from '~/composables/useTenantRole'
import { useRepositories } from '~/composables/useRepositories'
import type { MealPlan } from '~/repositories/types'
import type { ShoppingScope } from '~/composables/useShoppingList'
import { today } from '~/utils/dates'

definePageMeta({
  layout: 'default',
})

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const toast = useToast()
const { translateError } = useApiError()
const { formatDate, formatDateRange } = useFormatDate()
const { isMemberOrAbove, isCoordinatorOrAbove } = useTenantRole()
const tenantStore = useTenantStore()
const memberStore = useCurrentMemberStore()
const { mealPlans: mealRepo } = useRepositories()

const eventId = computed(() => route.params.eventId as string)
const { event, pending: eventPending } = useEvent(eventId)
const { report, pending: reportPending, error } = useEventReport(eventId)

const pending = computed(() => eventPending.value || reportPending.value)
const daysWithMeals = computed(() => (report.value?.days ?? []).filter(d => d.meals.length > 0))

// ── Day + meal selection: exactly one meal plan is in focus at a time. ───────
const selectedDay = ref<string>('')
const selectedPlanId = ref<string>('')

const dayOptions = computed(() =>
  daysWithMeals.value.map(d => ({ label: formatDate(d.day), value: d.day })))

const mealsForDay = computed(() =>
  daysWithMeals.value.find(d => d.day === selectedDay.value)?.meals ?? [])

const mealOptions = computed(() =>
  mealsForDay.value.map(m => ({
    label: `${t(`event.meal.${m.mealType.toLowerCase()}`)} · ${m.templateName}`,
    value: m.mealPlanId,
  })))

const selectedMeal = computed(() =>
  mealsForDay.value.find(m => m.mealPlanId === selectedPlanId.value) ?? null)

// Initial focus: a ?plan= deep link wins (dashboard widget), then ?day=, then the
// first day from today onward, then the event's first day with meals.
function initSelection() {
  if (selectedPlanId.value || !daysWithMeals.value.length) return
  const days = daysWithMeals.value

  const queryPlan = typeof route.query.plan === 'string' ? route.query.plan : null
  if (queryPlan) {
    const day = days.find(d => d.meals.some(m => m.mealPlanId === queryPlan))
    if (day) {
      selectedDay.value = day.day
      selectedPlanId.value = queryPlan
      return
    }
  }

  const queryDay = typeof route.query.day === 'string' ? route.query.day : null
  const initialDay = days.find(d => d.day === queryDay)
    ?? days.find(d => d.day >= today())
    ?? days[0]!
  selectedDay.value = initialDay.day
  selectedPlanId.value = initialDay.meals[0]!.mealPlanId
}
watch(report, initSelection, { immediate: true })

// Switching day refocuses on that day's first meal.
watch(selectedDay, () => {
  if (!mealsForDay.value.some(m => m.mealPlanId === selectedPlanId.value))
    selectedPlanId.value = mealsForDay.value[0]?.mealPlanId ?? ''
})

// Keep the selection shareable/bookmarkable (same pattern as the shopping page).
watch([selectedDay, selectedPlanId], ([day, plan]) => {
  if (!day || !plan) return
  void router.replace({ query: { ...route.query, day, plan } })
})

// Authoritative plan records — the notes form must pass isException/exceptionReason through
// unchanged on save (the report DTO omits the reason), so edits never touch plan structure.
const { data: planData, refresh: refreshPlans } = useAsyncData<MealPlan[]>(
  () => `meal-planner-plans-${tenantStore.currentTenantId}-${eventId.value}`,
  async () => {
    const tenantId = tenantStore.currentTenantId!
    const templates = await mealRepo.listMealTemplates(tenantId, eventId.value)
    const planLists = await Promise.all(
      templates.map(tpl => mealRepo.listPlans(tenantId, eventId.value, tpl.id).catch(() => [] as MealPlan[])),
    )
    return planLists.flat()
  },
  { watch: [eventId] },
)
const planById = computed(() => new Map((planData.value ?? []).map(p => [p.id, p])))

// Plans the current member volunteered to cook — one tenant-wide query gates every card
// (mirrors useShoppingList.canEditMealPlan; the backend enforces the same rule).
const { data: myMealsData } = useAsyncData(
  () => `meal-planner-my-meals-${tenantStore.currentTenantId}-${memberStore.linkedMemberId ?? 'none'}-${eventId.value}`,
  () => {
    const tenantId = tenantStore.currentTenantId
    const memberId = memberStore.linkedMemberId
    const fromDay = event.value?.startDate
    if (!tenantId || !memberId || !fromDay || isCoordinatorOrAbove.value) return Promise.resolve([])
    return mealRepo.listMyMeals(tenantId, memberId, fromDay)
  },
  { watch: [() => memberStore.linkedMemberId, () => event.value?.startDate] },
)
const volunteeredPlanIds = computed(() => new Set(
  (myMealsData.value ?? []).filter(m => m.eventId === eventId.value).map(m => m.mealPlanId),
))

function canEditPlan(planId: string): boolean {
  return isCoordinatorOrAbove.value || volunteeredPlanIds.value.has(planId)
}

const savingPlanIds = ref<string[]>([])

// The planner surfaces the notes it just saved (via refreshPlans), so no success toast.
async function saveNotes(planId: string, notes: string | null) {
  const plan = planById.value.get(planId)
  if (!plan) return
  savingPlanIds.value = [...savingPlanIds.value, planId]
  try {
    await mealRepo.updatePlan(
      tenantStore.currentTenantId!, eventId.value, plan.mealTemplateId, planId,
      notes, plan.isException ?? false, plan.exceptionReason ?? null,
    )
    await refreshPlans()
  }
  catch (e) {
    toast.add({ title: translateError(e), color: 'error' })
  }
  finally {
    savingPlanIds.value = savingPlanIds.value.filter(id => id !== planId)
  }
}

// Event scope without the property (staples stay on the shopping page); the mealPlanId
// prop narrows the embedded list to the focused meal only.
const shoppingScope = computed<ShoppingScope | null>(() =>
  event.value ? { kind: 'event', eventId: eventId.value, propertyId: null } : null)
</script>

<template>
  <div>
    <GsEmptyState
      v-if="!isMemberOrAbove"
      icon="i-heroicons-lock-closed"
      :title="t('report.noAccess')"
    />

    <div v-else-if="pending" class="py-16 text-center">
      <p class="text-muted">{{ t('common.loading') }}</p>
    </div>

    <GsEmptyState
      v-else-if="error"
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.fetchFailed')"
    />

    <template v-else-if="event && report">
      <GsBreadcrumb
        :items="[
          { label: t('event.title'), to: '/app/events' },
          { label: event.name, to: `/app/events/${eventId}` },
          { label: t('mealPlanner.title') },
        ]"
      />

      <GsPageHeader :title="t('mealPlanner.title')">
        <UButton
          variant="outline"
          size="sm"
          icon="i-heroicons-chart-bar"
          :to="`/app/reports/events/${eventId}#meals`"
        >
          {{ t('report.event.viewReport') }}
        </UButton>
        <UButton
          variant="outline"
          size="sm"
          icon="i-heroicons-calendar-days"
          :to="`/app/events/${eventId}`"
        >
          {{ t('report.event.viewSignup') }}
        </UButton>
      </GsPageHeader>

      <div class="flex items-center gap-2 text-sm text-muted mb-6 flex-wrap">
        <UIcon name="i-heroicons-cake" class="size-4 shrink-0" />
        <span>{{ `${event.name} · ${formatDateRange(event.startDate, event.endDate)}` }}</span>
      </div>

      <GsEmptyState
        v-if="!daysWithMeals.length"
        icon="i-heroicons-cake"
        :title="t('mealPlanner.noMeals')"
      />

      <template v-else>
        <!-- Focus pickers: one day, one meal — only that meal plan's details render below. -->
        <div class="flex items-center gap-3 flex-wrap mb-4">
          <USelect
            v-model="selectedDay"
            :items="dayOptions"
            size="sm"
            :icon="'i-heroicons-calendar-days'"
            class="min-w-44"
          />
          <USelect
            v-model="selectedPlanId"
            :items="mealOptions"
            size="sm"
            :icon="'i-heroicons-cake'"
            class="min-w-52"
          />
        </div>

        <GsMealPlannerCard
          v-if="selectedMeal"
          :key="selectedMeal.mealPlanId"
          :meal="selectedMeal"
          :plan="planById.get(selectedMeal.mealPlanId) ?? null"
          :can-edit="canEditPlan(selectedMeal.mealPlanId)"
          :saving="savingPlanIds.includes(selectedMeal.mealPlanId)"
          @save-notes="saveNotes(selectedMeal.mealPlanId, $event)"
        />

        <section v-if="selectedMeal" class="mt-8">
          <h2 class="text-sm font-semibold text-muted uppercase tracking-wider mb-3">
            {{ t('shopping.title') }}
          </h2>
          <GsShoppingList
            :scope="shoppingScope"
            :meal-plan-id="selectedPlanId"
            initial-mode="edit"
          />
        </section>
      </template>
    </template>

    <GsEmptyState
      v-else
      icon="i-heroicons-exclamation-triangle"
      :title="t('error.notFound')"
    />
  </div>
</template>

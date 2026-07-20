import { useTenantStore } from '~/stores/tenant'
import { useCurrentMemberStore } from '~/stores/member'
import { useTenantRole } from '~/composables/useTenantRole'
import type {
  ShoppingItem,
  ShoppingItemIntent,
  MealPlan,
  MealTemplate,
} from '~/repositories/types'
import type {
  CreateShoppingItemInput,
  UpdateShoppingItemInput,
  ShoppingItemIntentInput,
} from '~/repositories/interfaces'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'

/** Which list is being viewed/edited. Event scope also carries its property so trip staples merge in. */
export interface ShoppingScope {
  kind: 'event' | 'property'
  eventId: string | null
  propertyId: string | null
}

export interface ShoppingScopeOption {
  label: string
  propertyId?: string | null
  eventId?: string | null
  mealPlanId?: string | null
}

export interface ShoppingSection {
  id: string
  origin: 'Meal' | 'Event' | 'Property'
  title: string
  subtitle: string | null
  needByKey: string
  planId: string | null
  items: ShoppingItem[]
}

export const REFRESH_INTERVAL_S = 60

export function useShoppingList(scope: Ref<ShoppingScope | null>) {
  const tenantStore = useTenantStore()
  const memberStore = useCurrentMemberStore()
  const { isCoordinatorOrAbove } = useTenantRole()
  const { shoppingItems: repo, mealPlans: mealRepo } = useRepositories()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const { formatDate } = useFormatDate()
  const toast = useToast()

  const eventId = computed(() => (scope.value?.kind === 'event' ? scope.value.eventId : null))
  const propertyId = computed(() => scope.value?.propertyId ?? null)
  const myMemberId = computed(() => memberStore.linkedMemberId)

  const eventItems = ref<ShoppingItem[]>([])
  const propertyItems = ref<ShoppingItem[]>([])
  const planLabels = ref<Record<string, { title: string, subtitle: string }>>({})
  const mealScopeOptions = ref<ShoppingScopeOption[]>([])
  const volunteeredPlanIds = ref<Set<string>>(new Set())
  const pending = ref(false)
  const lastUpdatedAt = ref<number>(0)
  const updating = ref<string[]>([])

  // Loads meal-plan labels (for grouping) and, for non-coordinators, which plans the current
  // member volunteered for (so they may edit those meal lists). Only relevant for event scope.
  async function loadMealMeta() {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId || !eventId.value) {
      planLabels.value = {}
      mealScopeOptions.value = []
      volunteeredPlanIds.value = new Set()
      return
    }
    const evId = eventId.value
    const templates = await mealRepo.listMealTemplates(tenantId, evId).catch(() => [] as MealTemplate[])
    const nameById = Object.fromEntries(templates.map(tpl => [tpl.id, tpl.name ?? '']))
    const planLists = await Promise.all(
      templates.map(tpl =>
        mealRepo.listPlans(tenantId, evId, tpl.id)
          .then(plans => ({ templateId: tpl.id, plans }))
          .catch(() => ({ templateId: tpl.id, plans: [] as MealPlan[] })),
      ),
    )

    const labels: Record<string, { title: string, subtitle: string }> = {}
    const options: ShoppingScopeOption[] = []
    const volunteered = new Set<string>()
    const memberId = myMemberId.value
    const needsVolunteerCheck = !!memberId && !isCoordinatorOrAbove.value

    for (const { templateId, plans } of planLists) {
      for (const plan of plans) {
        const title = nameById[plan.mealTemplateId] ?? t('event.meal.title')
        const subtitle = `${formatDate(plan.day)} · ${t(`event.meal.${(plan.mealType ?? 'Dinner').toLowerCase()}`)}`
        labels[plan.id] = { title, subtitle }
        options.push({ label: `${title} — ${subtitle}`, mealPlanId: plan.id })
      }
      if (needsVolunteerCheck) {
        await Promise.all(plans.map(async (plan) => {
          try {
            const intents = await mealRepo.listIntentsForMember(tenantId, evId, templateId, plan.id, memberId!)
            // Any intent row means the member is a cook for this plan (row existence = sign-up).
            if (intents.some(i => i.householdMemberId === memberId)) volunteered.add(plan.id)
          }
          catch { /* best-effort gating; backend still enforces */ }
        }))
      }
    }
    options.sort((a, b) => a.label.localeCompare(b.label))
    planLabels.value = labels
    mealScopeOptions.value = options
    volunteeredPlanIds.value = volunteered
  }

  async function load() {
    const tenantId = tenantStore.currentTenantId
    const s = scope.value
    if (!tenantId || !s) return
    pending.value = true
    try {
      if (s.kind === 'event' && s.eventId) {
        const [evItems, prItems] = await Promise.all([
          repo.listByEvent(tenantId, s.eventId),
          s.propertyId ? repo.listByProperty(tenantId, s.propertyId) : Promise.resolve([] as ShoppingItem[]),
        ])
        eventItems.value = evItems
        propertyItems.value = prItems
      }
      else if (s.kind === 'property' && s.propertyId) {
        eventItems.value = []
        propertyItems.value = await repo.listByProperty(tenantId, s.propertyId)
      }
      lastUpdatedAt.value = Date.now()
    }
    finally {
      pending.value = false
    }
  }

  watch(scope, () => { void load(); void loadMealMeta() }, { immediate: true })

  // ── Staleness: poll on an interval and on tab re-focus. ──────────────────────
  let timer: ReturnType<typeof setInterval> | null = null
  function onVisible() {
    if (document.visibilityState === 'visible') void load()
  }
  onMounted(() => {
    // Skip the periodic poll while the tab is hidden to avoid wasting network requests;
    // onVisible refreshes immediately when the tab regains focus.
    timer = setInterval(() => {
      if (document.visibilityState === 'visible') void load()
    }, REFRESH_INTERVAL_S * 1000)
    document.addEventListener('visibilitychange', onVisible)
  })
  onUnmounted(() => {
    if (timer) clearInterval(timer)
    document.removeEventListener('visibilitychange', onVisible)
  })

  const allItems = computed<ShoppingItem[]>(() => [...eventItems.value, ...propertyItems.value])

  // Grouped, demarcated by origin (and by meal occurrence for meal items).
  const sections = computed<ShoppingSection[]>(() => {
    const result: ShoppingSection[] = []

    if (scope.value?.kind === 'event') {
      const mealGroups = new Map<string, ShoppingItem[]>()
      const eventOnly: ShoppingItem[] = []
      for (const item of eventItems.value) {
        if (item.origin === 'Meal' && item.mealPlanId) {
          const list = mealGroups.get(item.mealPlanId) ?? []
          list.push(item)
          mealGroups.set(item.mealPlanId, list)
        }
        else {
          eventOnly.push(item)
        }
      }

      const mealSections: ShoppingSection[] = []
      for (const [planId, items] of mealGroups) {
        const label = planLabels.value[planId]
        mealSections.push({
          id: `meal:${planId}`,
          origin: 'Meal',
          title: label?.title ?? t('event.meal.title'),
          subtitle: label?.subtitle ?? null,
          needByKey: items[0]?.neededByDate ?? '',
          planId,
          items: sortItems(items),
        })
      }
      mealSections.sort((a, b) => a.needByKey.localeCompare(b.needByKey))
      result.push(...mealSections)

      result.push({
        id: 'event',
        origin: 'Event',
        title: t('shopping.eventSupplies'),
        subtitle: null,
        needByKey: '~event',
        planId: null,
        items: sortItems(eventOnly),
      })
    }

    if (propertyId.value) {
      result.push({
        id: 'property',
        origin: 'Property',
        title: t('shopping.propertySupplies'),
        subtitle: null,
        needByKey: '~property',
        planId: null,
        items: sortItems(propertyItems.value),
      })
    }

    return result
  })

  // Date-first, then alphabetical. Status only styles a row — never reorders it — so an item
  // stays put when claimed/covered, in both Shop and Edit views.
  function sortItems(items: ShoppingItem[]): ShoppingItem[] {
    return [...items].sort((a, b) =>
      (a.neededByDate ?? '').localeCompare(b.neededByDate ?? '')
      || (a.name ?? '').localeCompare(b.name ?? ''),
    )
  }

  /** Quantity still outstanding (needed − provided), or null when the item is unquantified. */
  function remaining(item: ShoppingItem): number | null {
    if (item.quantityNeeded == null) return null
    return item.quantityNeeded - (item.quantityProvided ?? 0)
  }

  function patchLocal(updated: ShoppingItem) {
    for (const list of [eventItems, propertyItems]) {
      const idx = list.value.findIndex(i => i.id === updated.id)
      if (idx >= 0) list.value[idx] = updated
    }
  }

  /** The current member's own contribution to an item, if any. */
  function myIntent(item: ShoppingItem): ShoppingItemIntent | null {
    const id = myMemberId.value
    if (!id) return null
    return (item.intents ?? []).find(i => i.householdMemberId === id) ?? null
  }

  /** Whether the current member may edit/delete a meal-plan item (Coordinator+ or a volunteer). */
  function canEditMealPlan(planId: string | null): boolean {
    if (isCoordinatorOrAbove.value) return true
    return planId != null && volunteeredPlanIds.value.has(planId)
  }

  async function applyIntent(item: ShoppingItem, input: ShoppingItemIntentInput) {
    const memberId = myMemberId.value
    if (!memberId) {
      toast.add({ title: t('shopping.noMemberLink'), color: 'warning' })
      return
    }
    updating.value.push(item.id)
    try {
      const updated = await repo.upsertIntent(tenantStore.currentTenantId!, item.id, memberId, input)
      patchLocal(updated)
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(id => id !== item.id)
    }
  }

  function claim(item: ShoppingItem, quantity: number | null = null) {
    return applyIntent(item, { status: 'Claimed', quantity })
  }

  function provide(item: ShoppingItem, quantity: number | null = null) {
    return applyIntent(item, { status: 'Provided', quantity })
  }

  async function unclaim(item: ShoppingItem) {
    const memberId = myMemberId.value
    if (!memberId) return
    updating.value.push(item.id)
    try {
      const updated = await repo.removeIntent(tenantStore.currentTenantId!, item.id, memberId)
      patchLocal(updated)
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
    }
    finally {
      updating.value = updating.value.filter(id => id !== item.id)
    }
  }

  async function createItem(input: CreateShoppingItemInput): Promise<boolean> {
    updating.value.push('new')
    try {
      await repo.create(tenantStore.currentTenantId!, input)
      await load()
      return true
    }
    catch (e) {
      if (e instanceof DemoLimitError) {
        toast.add({ title: t('demo.limitReached.title'), description: t('demo.limitReached.description'), color: 'warning' })
        return false
      }
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== 'new')
    }
  }

  async function updateItem(itemId: string, input: UpdateShoppingItemInput): Promise<boolean> {
    updating.value.push(itemId)
    try {
      await repo.updateItem(tenantStore.currentTenantId!, itemId, input)
      await load()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== itemId)
    }
  }

  async function deleteItem(itemId: string): Promise<boolean> {
    updating.value.push(itemId)
    try {
      await repo.deleteItem(tenantStore.currentTenantId!, itemId)
      await load()
      return true
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
      return false
    }
    finally {
      updating.value = updating.value.filter(k => k !== itemId)
    }
  }

  return {
    sections,
    allItems,
    pending,
    updating,
    lastUpdatedAt,
    propertyId,
    mealScopeOptions,
    myMemberId,
    refresh: load,
    myIntent,
    remaining,
    canEditMealPlan,
    claim,
    provide,
    unclaim,
    createItem,
    updateItem,
    deleteItem,
  }
}

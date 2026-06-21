import { useTenantStore } from '~/stores/tenant'
import type {
  ShoppingItem,
  ShoppingItemStatus,
  MealPlan,
} from '~/repositories/types'
import type {
  CreateShoppingItemInput,
  UpdateShoppingItemInput,
} from '~/repositories/interfaces'
import { DemoLimitError } from '~/repositories/interfaces'
import { useRepositories } from '~/composables/useRepositories'
import { useEvent } from '~/composables/useEvents'
import { useMealTemplates } from '~/composables/useMealPlans'

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
  items: ShoppingItem[]
}

const REFRESH_INTERVAL_MS = 45_000

export function useShoppingList(eventId: Ref<string>) {
  const tenantStore = useTenantStore()
  const { shoppingItems: repo, mealPlans: mealRepo } = useRepositories()
  const { t } = useI18n()
  const { translateError } = useApiError()
  const toast = useToast()
  const { event } = useEvent(eventId)
  const { templates } = useMealTemplates(eventId)

  const propertyId = computed(() => event.value?.propertyId ?? null)

  const eventItems = ref<ShoppingItem[]>([])
  const propertyItems = ref<ShoppingItem[]>([])
  const planLabels = ref<Record<string, { title: string, subtitle: string }>>({})
  const mealScopeOptions = ref<ShoppingScopeOption[]>([])
  const pending = ref(false)
  const lastUpdatedAt = ref<number>(0)
  const updating = ref<string[]>([])

  const { formatDate } = useFormatDate()
  const templateNameById = computed<Record<string, string>>(() =>
    Object.fromEntries(templates.value.map(tpl => [tpl.id, tpl.name ?? ''])),
  )

  async function loadPlanLabels() {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId || !templates.value.length) {
      planLabels.value = {}
      mealScopeOptions.value = []
      return
    }
    const planLists = await Promise.all(
      templates.value.map(tpl => mealRepo.listPlans(tenantId, eventId.value, tpl.id).catch(() => [] as MealPlan[])),
    )
    const labels: Record<string, { title: string, subtitle: string }> = {}
    const options: ShoppingScopeOption[] = []
    for (const plans of planLists) {
      for (const plan of plans) {
        const title = templateNameById.value[plan.mealTemplateId] ?? t('event.meal.title')
        const subtitle = `${formatDate(plan.day)} · ${t(`event.meal.${(plan.mealType ?? 'Dinner').toLowerCase()}`)}`
        labels[plan.id] = { title, subtitle }
        options.push({ label: `${title} — ${subtitle}`, mealPlanId: plan.id })
      }
    }
    options.sort((a, b) => a.label.localeCompare(b.label))
    planLabels.value = labels
    mealScopeOptions.value = options
  }

  async function load() {
    const tenantId = tenantStore.currentTenantId
    if (!tenantId || !eventId.value) return
    pending.value = true
    try {
      const [evItems, prItems] = await Promise.all([
        repo.listByEvent(tenantId, eventId.value),
        propertyId.value ? repo.listByProperty(tenantId, propertyId.value) : Promise.resolve([] as ShoppingItem[]),
      ])
      eventItems.value = evItems
      propertyItems.value = prItems
      lastUpdatedAt.value = Date.now()
    }
    finally {
      pending.value = false
    }
  }

  // Reload when the event/property resolves; rebuild meal labels when templates change.
  watch([eventId, propertyId], () => { void load() }, { immediate: true })
  watch(templates, () => { void loadPlanLabels() }, { immediate: true })

  // ── Staleness: poll on an interval and on tab re-focus (kind to mobile data —
  //    only the active event's two scopes are refetched). ──────────────────────
  let timer: ReturnType<typeof setInterval> | null = null
  function onVisible() {
    if (document.visibilityState === 'visible') void load()
  }
  onMounted(() => {
    timer = setInterval(() => { void load() }, REFRESH_INTERVAL_MS)
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

    for (const [planId, items] of mealGroups) {
      const label = planLabels.value[planId]
      result.push({
        id: `meal:${planId}`,
        origin: 'Meal',
        title: label?.title ?? t('event.meal.title'),
        subtitle: label?.subtitle ?? null,
        needByKey: items[0]?.neededByDate ?? '',
        items: sortItems(items),
      })
    }
    result.sort((a, b) => a.needByKey.localeCompare(b.needByKey))

    result.push({
      id: 'event',
      origin: 'Event',
      title: t('shopping.eventSupplies'),
      subtitle: null,
      needByKey: '~event',
      items: sortItems(eventOnly),
    })
    result.push({
      id: 'property',
      origin: 'Property',
      title: t('shopping.propertySupplies'),
      subtitle: null,
      needByKey: '~property',
      items: sortItems(propertyItems.value),
    })

    return result
  })

  function sortItems(items: ShoppingItem[]): ShoppingItem[] {
    const statusRank: Record<ShoppingItemStatus, number> = { Needed: 0, Claimed: 1, Covered: 2 }
    return [...items].sort((a, b) =>
      (statusRank[a.status ?? 'Needed'] - statusRank[b.status ?? 'Needed'])
      || (a.name ?? '').localeCompare(b.name ?? ''),
    )
  }

  function patchLocal(updated: ShoppingItem) {
    for (const list of [eventItems, propertyItems]) {
      const idx = list.value.findIndex(i => i.id === updated.id)
      if (idx >= 0) list.value[idx] = updated
    }
  }

  async function setFulfillment(item: ShoppingItem, status: ShoppingItemStatus, quantityProvided: number | null, claimedByMemberId: string | null) {
    updating.value.push(item.id)
    try {
      const updated = await repo.updateFulfillment(tenantStore.currentTenantId!, item.id, status, quantityProvided, claimedByMemberId)
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

  async function deleteItem(itemId: string): Promise<void> {
    updating.value.push(itemId)
    try {
      await repo.deleteItem(tenantStore.currentTenantId!, itemId)
      await load()
    }
    catch (e) {
      toast.add({ title: translateError(e), color: 'error' })
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
    refresh: load,
    setFulfillment,
    createItem,
    updateItem,
    deleteItem,
  }
}

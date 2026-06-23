import type {
  IShoppingItemRepository,
  CreateShoppingItemInput,
  UpdateShoppingItemInput,
  ShoppingItemIntentInput,
} from '../interfaces'
import type {
  ShoppingItem,
  ShoppingItemOrigin,
  ShoppingItemStatus,
  ShoppingItemIntent,
  AttributeWriteEntry,
  AttributeEntry,
} from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

/** Mirrors the backend DeriveFulfillment: status + provided total are derived from live intents. */
function deriveFulfillment(quantityNeeded: number | null, intents: ShoppingItemIntent[]): { status: ShoppingItemStatus, quantityProvided: number | null } {
  if (intents.length === 0) return { status: 'Needed', quantityProvided: null }

  let providedQty = 0
  let hasProvided = false
  let coversWholeNeed = false
  for (const intent of intents) {
    if (intent.status !== 'Provided') continue
    hasProvided = true
    if (intent.quantity != null) providedQty += intent.quantity
    else coversWholeNeed = true
  }

  const quantityProvided = hasProvided ? providedQty : null
  let status: ShoppingItemStatus
  if (quantityNeeded != null && quantityNeeded > 0)
    status = coversWholeNeed || providedQty >= quantityNeeded ? 'Covered' : 'Claimed'
  else
    status = hasProvided ? 'Covered' : 'Claimed'

  return { status, quantityProvided }
}

/** Re-applies derived fields to an item after its intents change. */
function applyFulfillment(item: ShoppingItem): ShoppingItem {
  const { status, quantityProvided } = deriveFulfillment(item.quantityNeeded ?? null, item.intents ?? [])
  item.status = status
  item.quantityProvided = quantityProvided
  return item
}

export class DemoShoppingItemRepository implements IShoppingItemRepository {
  async listByEvent(tenantId: string, eventId: string): Promise<ShoppingItem[]> {
    return getDemoStore().shoppingItems.value.filter(i => i.tenantId === tenantId && i.eventId === eventId)
  }

  async listByProperty(tenantId: string, propertyId: string): Promise<ShoppingItem[]> {
    return getDemoStore().shoppingItems.value.filter(i => i.tenantId === tenantId && i.propertyId === propertyId)
  }

  async create(tenantId: string, input: CreateShoppingItemInput): Promise<ShoppingItem> {
    const store = getDemoStore()
    if (store.shoppingItems.value.filter(i => i.tenantId === tenantId).length >= DEMO_LIMITS.shoppingItemsPerTenant) {
      throw new DemoLimitError('shoppingItemsPerTenant')
    }

    let origin: ShoppingItemOrigin
    let propertyId: string | null = null
    let eventId: string | null = null
    let mealPlanId: string | null = null
    let neededByDate: string | null = input.neededByDate ?? null

    if (input.propertyId) {
      origin = 'Property'
      propertyId = input.propertyId
    }
    else if (input.eventId) {
      origin = 'Event'
      eventId = input.eventId
    }
    else {
      origin = 'Meal'
      mealPlanId = input.mealPlanId!
      // Meal items derive their event + need date from the plan (mirrors the API).
      const plan = store.mealPlans.value.find(p => p.id === mealPlanId)
      const template = store.mealTemplates.value.find(t => t.id === plan?.mealTemplateId)
      eventId = template?.eventId ?? null
      neededByDate = plan?.day ?? null
    }

    const item: ShoppingItem = {
      id: demoId(),
      tenantId,
      origin,
      propertyId,
      eventId,
      mealPlanId,
      name: input.name,
      quantityNeeded: input.quantityNeeded ?? null,
      unit: input.unit ?? null,
      quantityProvided: null,
      status: 'Needed',
      neededByDate,
      category: input.category ?? null,
      notes: input.notes ?? null,
      attributes: toAttributeEntries(input.attributes),
      intents: [],
    }
    store.shoppingItems.value.push(item)
    persistDemoStore()
    return item
  }

  async updateItem(_tenantId: string, itemId: string, input: UpdateShoppingItemInput): Promise<void> {
    const store = getDemoStore()
    const item = store.shoppingItems.value.find(i => i.id === itemId)
    if (!item) return
    item.name = input.name
    item.quantityNeeded = input.quantityNeeded ?? null
    item.unit = input.unit ?? null
    item.category = input.category ?? null
    item.notes = input.notes ?? null
    // Meal items keep their plan-derived need date; only manual-scope items accept an override.
    if (item.origin !== 'Meal') item.neededByDate = input.neededByDate ?? null
    if (input.attributes !== undefined) item.attributes = toAttributeEntries(input.attributes)
    persistDemoStore()
  }

  async upsertIntent(_tenantId: string, itemId: string, memberId: string, input: ShoppingItemIntentInput): Promise<ShoppingItem> {
    const store = getDemoStore()
    const item = store.shoppingItems.value.find(i => i.id === itemId)
    if (!item) throw new Error('Shopping item not found')
    item.intents = item.intents ?? []
    // One intent per member: update in place, otherwise append.
    const existing = item.intents.find(x => x.householdMemberId === memberId)
    if (existing) {
      existing.quantity = input.quantity ?? null
      existing.status = input.status
      existing.notes = input.notes ?? null
    }
    else {
      item.intents.push({
        id: demoId(),
        householdMemberId: memberId,
        quantity: input.quantity ?? null,
        status: input.status,
        notes: input.notes ?? null,
      })
    }
    applyFulfillment(item)
    persistDemoStore()
    return item
  }

  async removeIntent(_tenantId: string, itemId: string, memberId: string): Promise<ShoppingItem> {
    const store = getDemoStore()
    const item = store.shoppingItems.value.find(i => i.id === itemId)
    if (!item) throw new Error('Shopping item not found')
    item.intents = (item.intents ?? []).filter(x => x.householdMemberId !== memberId)
    applyFulfillment(item)
    persistDemoStore()
    return item
  }

  async deleteItem(_tenantId: string, itemId: string): Promise<void> {
    const store = getDemoStore()
    store.shoppingItems.value = store.shoppingItems.value.filter(i => i.id !== itemId)
    persistDemoStore()
  }
}

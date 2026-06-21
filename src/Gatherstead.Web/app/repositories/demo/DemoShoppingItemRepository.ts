import type {
  IShoppingItemRepository,
  CreateShoppingItemInput,
  UpdateShoppingItemInput,
} from '../interfaces'
import type {
  ShoppingItem,
  ShoppingItemOrigin,
  ShoppingItemStatus,
  AttributeWriteEntry,
  AttributeEntry,
} from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
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
      claimedByMemberId: null,
      neededByDate,
      category: input.category ?? null,
      notes: input.notes ?? null,
      attributes: toAttributeEntries(input.attributes),
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

  async updateFulfillment(
    _tenantId: string,
    itemId: string,
    status: ShoppingItemStatus,
    quantityProvided: number | null,
    claimedByMemberId: string | null,
  ): Promise<ShoppingItem> {
    const store = getDemoStore()
    const item = store.shoppingItems.value.find(i => i.id === itemId)
    if (!item) throw new Error('Shopping item not found')
    item.status = status
    item.quantityProvided = quantityProvided
    item.claimedByMemberId = claimedByMemberId
    persistDemoStore()
    return item
  }

  async deleteItem(_tenantId: string, itemId: string): Promise<void> {
    const store = getDemoStore()
    store.shoppingItems.value = store.shoppingItems.value.filter(i => i.id !== itemId)
    persistDemoStore()
  }
}

import type {
  IShoppingItemRepository,
  CreateShoppingItemInput,
  UpdateShoppingItemInput,
  ShoppingItemIntentInput,
} from '../interfaces'
import type { ShoppingItem } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveShoppingItemRepository implements IShoppingItemRepository {
  async listByEvent(tenantId: string, eventId: string): Promise<ShoppingItem[]> {
    const r = await $fetch<ApiResponse<ShoppingItem[]>>(
      `/api/proxy/tenants/${tenantId}/shopping-items?eventId=${eventId}`,
    )
    return r.entity ?? []
  }

  async listByProperty(tenantId: string, propertyId: string): Promise<ShoppingItem[]> {
    const r = await $fetch<ApiResponse<ShoppingItem[]>>(
      `/api/proxy/tenants/${tenantId}/shopping-items?propertyId=${propertyId}`,
    )
    return r.entity ?? []
  }

  async create(tenantId: string, input: CreateShoppingItemInput): Promise<ShoppingItem> {
    const r = await $fetch<ApiResponse<ShoppingItem>>(
      `/api/proxy/tenants/${tenantId}/shopping-items`,
      { method: 'POST', body: { ...input, attributes: input.attributes ?? null } },
    )
    return r.entity
  }

  async updateItem(tenantId: string, itemId: string, input: UpdateShoppingItemInput): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/shopping-items/${itemId}`, {
      method: 'PUT',
      body: { ...input, attributes: input.attributes ?? null },
    })
  }

  async upsertIntent(tenantId: string, itemId: string, memberId: string, input: ShoppingItemIntentInput): Promise<ShoppingItem> {
    const r = await $fetch<ApiResponse<ShoppingItem>>(
      `/api/proxy/tenants/${tenantId}/shopping-items/${itemId}/intents/${memberId}`,
      { method: 'PUT', body: { quantity: input.quantity ?? null, status: input.status, notes: input.notes ?? null } },
    )
    return r.entity
  }

  async removeIntent(tenantId: string, itemId: string, memberId: string): Promise<ShoppingItem> {
    const r = await $fetch<ApiResponse<ShoppingItem>>(
      `/api/proxy/tenants/${tenantId}/shopping-items/${itemId}/intents/${memberId}`,
      { method: 'DELETE' },
    )
    return r.entity
  }

  async deleteItem(tenantId: string, itemId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/shopping-items/${itemId}`, { method: 'DELETE' })
  }
}

import type {
  IShoppingItemRepository,
  CreateShoppingItemInput,
  UpdateShoppingItemInput,
} from '../interfaces'
import type { ShoppingItem, ShoppingItemStatus } from '../types'

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

  async updateFulfillment(
    tenantId: string,
    itemId: string,
    status: ShoppingItemStatus,
    quantityProvided: number | null,
    claimedByMemberId: string | null,
  ): Promise<ShoppingItem> {
    const r = await $fetch<ApiResponse<ShoppingItem>>(
      `/api/proxy/tenants/${tenantId}/shopping-items/${itemId}/fulfillment`,
      { method: 'PUT', body: { status, quantityProvided, claimedByMemberId } },
    )
    return r.entity
  }

  async deleteItem(tenantId: string, itemId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/shopping-items/${itemId}`, { method: 'DELETE' })
  }
}

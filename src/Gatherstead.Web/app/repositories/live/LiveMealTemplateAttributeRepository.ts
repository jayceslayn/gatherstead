import type { IMealTemplateAttributeRepository } from '../interfaces'
import type { MealTemplateAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveMealTemplateAttributeRepository implements IMealTemplateAttributeRepository {
  async listAttributes(tenantId: string, mealTemplateId: string): Promise<MealTemplateAttribute[]> {
    const r = await $fetch<ApiResponse<MealTemplateAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/meal-templates/${mealTemplateId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, mealTemplateId: string, attributeId: string): Promise<MealTemplateAttribute | null> {
    const r = await $fetch<ApiResponse<MealTemplateAttribute>>(
      `/api/proxy/tenants/${tenantId}/meal-templates/${mealTemplateId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, mealTemplateId: string, key: string, value: string, tenantMinRole: number): Promise<MealTemplateAttribute> {
    const r = await $fetch<ApiResponse<MealTemplateAttribute>>(
      `/api/proxy/tenants/${tenantId}/meal-templates/${mealTemplateId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, mealTemplateId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/meal-templates/${mealTemplateId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole },
    })
  }

  async deleteAttribute(tenantId: string, mealTemplateId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/meal-templates/${mealTemplateId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}

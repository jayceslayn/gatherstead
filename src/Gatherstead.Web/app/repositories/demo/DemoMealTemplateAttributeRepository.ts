import type { IMealTemplateAttributeRepository } from '../interfaces'
import type { MealTemplateAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoMealTemplateAttributeRepository implements IMealTemplateAttributeRepository {
  async listAttributes(tenantId: string, mealTemplateId: string): Promise<MealTemplateAttribute[]> {
    return getDemoStore().mealTemplateAttributes.value.filter(
      a => a.tenantId === tenantId && a.parentId === mealTemplateId,
    )
  }

  async getAttribute(tenantId: string, mealTemplateId: string, attributeId: string): Promise<MealTemplateAttribute | null> {
    return getDemoStore().mealTemplateAttributes.value.find(
      a => a.tenantId === tenantId && a.parentId === mealTemplateId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, mealTemplateId: string, key: string, value: string, tenantMinRole: number): Promise<MealTemplateAttribute> {
    const store = getDemoStore()
    const a: MealTemplateAttribute = { id: demoId(), tenantId, parentId: mealTemplateId, key, value, tenantMinRole, householdMinRole: null }
    store.mealTemplateAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, mealTemplateId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    const store = getDemoStore()
    const a = store.mealTemplateAttributes.value.find(
      x => x.tenantId === tenantId && x.parentId === mealTemplateId && x.id === attributeId,
    )
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, mealTemplateId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.mealTemplateAttributes.value = store.mealTemplateAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.parentId === mealTemplateId && a.id === attributeId),
    )
    persistDemoStore()
  }
}

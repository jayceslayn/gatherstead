import type { ITaskTemplateAttributeRepository } from '../interfaces'
import type { TaskTemplateAttribute } from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoTaskTemplateAttributeRepository implements ITaskTemplateAttributeRepository {
  async listAttributes(tenantId: string, taskTemplateId: string): Promise<TaskTemplateAttribute[]> {
    return getDemoStore().taskTemplateAttributes.value.filter(
      a => a.tenantId === tenantId && a.parentId === taskTemplateId,
    )
  }

  async getAttribute(tenantId: string, taskTemplateId: string, attributeId: string): Promise<TaskTemplateAttribute | null> {
    return getDemoStore().taskTemplateAttributes.value.find(
      a => a.tenantId === tenantId && a.parentId === taskTemplateId && a.id === attributeId,
    ) ?? null
  }

  async createAttribute(tenantId: string, taskTemplateId: string, key: string, value: string, tenantMinRole: number): Promise<TaskTemplateAttribute> {
    const store = getDemoStore()
    const a: TaskTemplateAttribute = { id: demoId(), tenantId, parentId: taskTemplateId, key, value, tenantMinRole, householdMinRole: null }
    store.taskTemplateAttributes.value.push(a)
    persistDemoStore()
    return a
  }

  async updateAttribute(tenantId: string, taskTemplateId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    const store = getDemoStore()
    const a = store.taskTemplateAttributes.value.find(
      x => x.tenantId === tenantId && x.parentId === taskTemplateId && x.id === attributeId,
    )
    if (!a) return
    a.key = key
    a.value = value
    a.tenantMinRole = tenantMinRole
    persistDemoStore()
  }

  async deleteAttribute(tenantId: string, taskTemplateId: string, attributeId: string): Promise<void> {
    const store = getDemoStore()
    store.taskTemplateAttributes.value = store.taskTemplateAttributes.value.filter(
      a => !(a.tenantId === tenantId && a.parentId === taskTemplateId && a.id === attributeId),
    )
    persistDemoStore()
  }
}

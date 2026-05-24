import type { ITaskTemplateAttributeRepository } from '../interfaces'
import type { TaskTemplateAttribute } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveTaskTemplateAttributeRepository implements ITaskTemplateAttributeRepository {
  async listAttributes(tenantId: string, taskTemplateId: string): Promise<TaskTemplateAttribute[]> {
    const r = await $fetch<ApiResponse<TaskTemplateAttribute[]>>(
      `/api/proxy/tenants/${tenantId}/task-templates/${taskTemplateId}/attributes`,
    )
    return r.entity ?? []
  }

  async getAttribute(tenantId: string, taskTemplateId: string, attributeId: string): Promise<TaskTemplateAttribute | null> {
    const r = await $fetch<ApiResponse<TaskTemplateAttribute>>(
      `/api/proxy/tenants/${tenantId}/task-templates/${taskTemplateId}/attributes/${attributeId}`,
    )
    return r.entity ?? null
  }

  async createAttribute(tenantId: string, taskTemplateId: string, key: string, value: string, tenantMinRole: number): Promise<TaskTemplateAttribute> {
    const r = await $fetch<ApiResponse<TaskTemplateAttribute>>(
      `/api/proxy/tenants/${tenantId}/task-templates/${taskTemplateId}/attributes`,
      { method: 'POST', body: { key, value, tenantMinRole } },
    )
    return r.entity
  }

  async updateAttribute(tenantId: string, taskTemplateId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/task-templates/${taskTemplateId}/attributes/${attributeId}`, {
      method: 'PUT',
      body: { key, value, tenantMinRole },
    })
  }

  async deleteAttribute(tenantId: string, taskTemplateId: string, attributeId: string): Promise<void> {
    await $fetch(`/api/proxy/tenants/${tenantId}/task-templates/${taskTemplateId}/attributes/${attributeId}`, { method: 'DELETE' })
  }
}

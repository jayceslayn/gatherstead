import type { ITaskRepository } from '../interfaces'
import type { TaskTemplate, TaskPlan, TaskIntent, AttributeWriteEntry } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveTaskRepository implements ITaskRepository {
  async listTaskTemplates(tenantId: string, eventId: string): Promise<TaskTemplate[]> {
    const r = await $fetch<ApiResponse<TaskTemplate[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates`,
    )
    return r.entity ?? []
  }

  async listPlans(tenantId: string, eventId: string, templateId: string): Promise<TaskPlan[]> {
    const r = await $fetch<ApiResponse<TaskPlan[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}/plans`,
    )
    return r.entity ?? []
  }

  async listIntentsForMember(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    memberId: string,
  ): Promise<TaskIntent[]> {
    const r = await $fetch<ApiResponse<TaskIntent[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}/plans/${planId}/intents?memberIds=${encodeURIComponent(memberId)}`,
    )
    return r.entity ?? []
  }

  async listPlanIntents(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
  ): Promise<TaskIntent[]> {
    const r = await $fetch<ApiResponse<TaskIntent[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}/plans/${planId}/intents`,
    )
    return r.entity ?? []
  }

  async upsertIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    householdId: string,
    memberId: string,
    volunteered: boolean,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}/plans/${planId}/intents?householdId=${encodeURIComponent(householdId)}`,
      {
        method: 'PUT',
        body: { householdMemberId: memberId, volunteered },
      },
    )
  }

  async createTemplate(
    tenantId: string,
    eventId: string,
    name: string,
    timeSlots: number,
    startDate: string | null,
    endDate: string | null,
    minimumAssignees: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<TaskTemplate> {
    const r = await $fetch<ApiResponse<TaskTemplate>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates`,
      { method: 'POST', body: { name, timeSlots, startDate, endDate, minimumAssignees, notes, attributes: attributes ?? null } },
    )
    return r.entity
  }

  async updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    timeSlots: number,
    startDate: string | null,
    endDate: string | null,
    minimumAssignees: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}`,
      { method: 'PUT', body: { name, timeSlots, startDate, endDate, minimumAssignees, notes, attributes: attributes ?? null } },
    )
  }

  async deleteTemplate(tenantId: string, eventId: string, templateId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}`,
      { method: 'DELETE' },
    )
  }

  async deleteIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    intentId: string,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}/plans/${planId}/intents/${intentId}`,
      { method: 'DELETE' },
    )
  }
}

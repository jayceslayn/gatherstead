import type { BulkTaskIntentItem, ITaskRepository } from '../interfaces'
import type { TaskTemplate, TaskPlan, TaskIntent, MyTask, AttributeWriteEntry } from '../types'
import { trackPersistence } from '../../utils/telemetry'
import { retryOn429 } from './retryOn429'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveTaskRepository implements ITaskRepository {
  async listMyTasks(tenantId: string, memberId: string, fromDay: string): Promise<MyTask[]> {
    const params = new URLSearchParams({ memberIds: memberId, fromDay })
    const r = await $fetch<ApiResponse<MyTask[]>>(
      `/api/proxy/tenants/${tenantId}/task-intents?${params.toString()}`,
    )
    return r.entity ?? []
  }

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

  async listEventIntents(tenantId: string, eventId: string): Promise<TaskIntent[]> {
    const r = await $fetch<ApiResponse<TaskIntent[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-intents`,
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
    trackPersistence('task_volunteer', 'set', { volunteered: volunteered ? 1 : 0 })
  }

  async bulkUpsertIntents(
    tenantId: string,
    eventId: string,
    items: BulkTaskIntentItem[],
  ): Promise<void> {
    if (!items.length) return
    await retryOn429(() => $fetch<unknown>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-intents/bulk`,
      {
        method: 'PUT',
        body: {
          items: items.map(i => ({ taskPlanId: i.planId, householdMemberId: i.memberId, volunteered: i.volunteered })),
        },
      },
    ))
    trackPersistence('task_volunteer', 'set', { count: items.length })
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
    trackPersistence('task_template', 'create')
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
    trackPersistence('task_template', 'update')
  }

  async deleteTemplate(tenantId: string, eventId: string, templateId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}`,
      { method: 'DELETE' },
    )
  }

  async updatePlan(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    completed: boolean,
    notes: string | null,
    isException: boolean,
    exceptionReason: string | null,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}/plans/${planId}`,
      { method: 'PUT', body: { completed, notes, isException, exceptionReason } },
    )
    trackPersistence('task_plan', 'update', { completed: completed ? 1 : 0, isException: isException ? 1 : 0 })
  }

  async deletePlan(tenantId: string, eventId: string, templateId: string, planId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/task-templates/${templateId}/plans/${planId}`,
      { method: 'DELETE' },
    )
    trackPersistence('task_plan', 'delete')
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
    trackPersistence('task_intent', 'delete')
  }
}

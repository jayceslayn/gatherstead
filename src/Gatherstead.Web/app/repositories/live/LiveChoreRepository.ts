import type { IChoreRepository } from '../interfaces'
import type { ChoreTemplate, ChorePlan, ChoreIntent } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveChoreRepository implements IChoreRepository {
  async listChoreTemplates(tenantId: string, eventId: string): Promise<ChoreTemplate[]> {
    const r = await $fetch<ApiResponse<ChoreTemplate[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates`,
    )
    return r.entity ?? []
  }

  async listPlans(tenantId: string, eventId: string, templateId: string): Promise<ChorePlan[]> {
    const r = await $fetch<ApiResponse<ChorePlan[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates/${templateId}/plans`,
    )
    return r.entity ?? []
  }

  async listIntentsForMember(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    memberId: string,
  ): Promise<ChoreIntent[]> {
    const r = await $fetch<ApiResponse<ChoreIntent[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates/${templateId}/plans/${planId}/intents?memberIds=${encodeURIComponent(memberId)}`,
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
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates/${templateId}/plans/${planId}/intents?householdId=${encodeURIComponent(householdId)}`,
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
    minimumAssignees: number | null,
    notes: string | null,
  ): Promise<ChoreTemplate> {
    const r = await $fetch<ApiResponse<ChoreTemplate>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates`,
      { method: 'POST', body: { name, timeSlots, minimumAssignees, notes } },
    )
    return r.entity
  }

  async updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    timeSlots: number,
    minimumAssignees: number | null,
    notes: string | null,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates/${templateId}`,
      { method: 'PUT', body: { name, timeSlots, minimumAssignees, notes } },
    )
  }

  async deleteTemplate(tenantId: string, eventId: string, templateId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates/${templateId}`,
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
      `/api/proxy/tenants/${tenantId}/events/${eventId}/chore-templates/${templateId}/plans/${planId}/intents/${intentId}`,
      { method: 'DELETE' },
    )
  }
}

import type { IMealPlanRepository } from '../interfaces'
import type { MealTemplate, MealPlan, MealIntent, MealIntentStatus } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveMealPlanRepository implements IMealPlanRepository {
  async listMealTemplates(tenantId: string, eventId: string): Promise<MealTemplate[]> {
    const r = await $fetch<ApiResponse<MealTemplate[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-templates`,
    )
    return r.entity ?? []
  }

  async listPlans(tenantId: string, eventId: string, templateId: string): Promise<MealPlan[]> {
    const r = await $fetch<ApiResponse<MealPlan[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-templates/${templateId}/plans`,
    )
    return r.entity ?? []
  }

  async listIntentsForMember(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    memberId: string,
  ): Promise<MealIntent[]> {
    const r = await $fetch<ApiResponse<MealIntent[]>>(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-templates/${templateId}/plans/${planId}/intents?memberIds=${encodeURIComponent(memberId)}`,
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
    status: MealIntentStatus,
    bringOwnFood: boolean,
  ): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/events/${eventId}/meal-templates/${templateId}/plans/${planId}/intents?householdId=${encodeURIComponent(householdId)}`,
      {
        method: 'PUT',
        body: { householdMemberId: memberId, status, bringOwnFood },
      },
    )
  }
}

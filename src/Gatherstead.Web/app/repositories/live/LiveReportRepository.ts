import type { IReportRepository } from '../interfaces'
import type { EventReport } from '../types'

interface ApiResponse<T> { entity: T; successful: boolean }

export class LiveReportRepository implements IReportRepository {
  async getEventMealReport(tenantId: string, eventId: string): Promise<EventReport | null> {
    const r = await $fetch<ApiResponse<EventReport>>(
      `/api/proxy/tenants/${tenantId}/reports/events/${eventId}`,
    )
    return r.entity ?? null
  }
}

import type { IHouseholdRepository } from '../interfaces'
import type { HouseholdSummary } from '../types'

interface HouseholdsApiResponse {
  entity: HouseholdSummary[]
  successful: boolean
}

interface HouseholdApiResponse {
  entity: HouseholdSummary
  successful: boolean
}

export class LiveHouseholdRepository implements IHouseholdRepository {
  async listHouseholds(tenantId: string): Promise<HouseholdSummary[]> {
    const r = await $fetch<HouseholdsApiResponse>(`/api/proxy/tenants/${tenantId}/households`)
    return r.entity ?? []
  }

  async getHousehold(tenantId: string, householdId: string): Promise<HouseholdSummary | null> {
    const r = await $fetch<HouseholdApiResponse>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}`,
    )
    return r.entity ?? null
  }
}

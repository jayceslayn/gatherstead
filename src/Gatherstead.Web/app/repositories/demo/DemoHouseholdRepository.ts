import type { IHouseholdRepository } from '../interfaces'
import type { HouseholdSummary } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

export class DemoHouseholdRepository implements IHouseholdRepository {
  async listHouseholds(tenantId: string): Promise<HouseholdSummary[]> {
    return getDemoStore().households.value.filter(h => h.tenantId === tenantId)
  }

  async getHousehold(tenantId: string, householdId: string): Promise<HouseholdSummary | null> {
    return getDemoStore().households.value.find(
      h => h.tenantId === tenantId && h.id === householdId,
    ) ?? null
  }

  async createHousehold(tenantId: string, name: string): Promise<HouseholdSummary> {
    const store = getDemoStore()
    if (store.households.value.filter(h => h.tenantId === tenantId).length >= DEMO_LIMITS.householdsPerTenant) {
      throw new DemoLimitError('householdsPerTenant')
    }
    const h: HouseholdSummary = { id: demoId(), tenantId, name }
    store.households.value.push(h)
    persistDemoStore()
    return h
  }
}

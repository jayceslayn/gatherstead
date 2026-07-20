import type { IHouseholdRepository } from '../interfaces'
import type { HouseholdSummary, AttributeWriteEntry, AttributeEntry } from '../types'
import { getDemoStore, persistDemoStore, demoId, DEMO_LIMITS, DemoLimitError } from './DemoStore'

function toAttributeEntries(writes: AttributeWriteEntry[] | null | undefined): AttributeEntry[] {
  if (!writes) return []
  return writes.map(w => ({ id: demoId(), key: w.key, value: w.value, tenantMinRole: w.tenantMinRole, householdMinRole: w.householdMinRole ?? null }))
}

export class DemoHouseholdRepository implements IHouseholdRepository {
  async listHouseholds(tenantId: string): Promise<HouseholdSummary[]> {
    return getDemoStore().households.value.filter(h => h.tenantId === tenantId)
  }

  async getHousehold(tenantId: string, householdId: string): Promise<HouseholdSummary | null> {
    return getDemoStore().households.value.find(
      h => h.tenantId === tenantId && h.id === householdId,
    ) ?? null
  }

  async createHousehold(tenantId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<HouseholdSummary> {
    const store = getDemoStore()
    if (store.households.value.filter(h => h.tenantId === tenantId).length >= DEMO_LIMITS.householdsPerTenant) {
      throw new DemoLimitError('householdsPerTenant')
    }
    // The demo persona is a tenant Owner with no per-household grant, so callerRole is null —
    // mirrors the API, which returns null when the caller has no HouseholdUser row.
    const h: HouseholdSummary = { id: demoId(), tenantId, name, notes: notes ?? null, attributes: toAttributeEntries(attributes), callerRole: null }
    store.households.value.push(h)
    persistDemoStore()
    return h
  }

  async updateHousehold(_tenantId: string, householdId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<void> {
    const store = getDemoStore()
    const h = store.households.value.find(x => x.id === householdId)
    if (!h) return
    h.name = name
    h.notes = notes ?? null
    if (attributes !== undefined) h.attributes = toAttributeEntries(attributes)
    persistDemoStore()
  }

  async deleteHousehold(_tenantId: string, householdId: string): Promise<void> {
    const store = getDemoStore()
    store.members.value = store.members.value.filter(m => m.householdId !== householdId)
    store.households.value = store.households.value.filter(h => h.id !== householdId)
    persistDemoStore()
  }
}

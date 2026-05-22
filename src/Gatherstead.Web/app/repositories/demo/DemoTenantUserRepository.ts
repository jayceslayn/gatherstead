import type { HouseholdRole, HouseholdUserSummary, TenantRole, TenantUserSummary } from '../types'
import type { ITenantUserRepository } from '../interfaces'
import { getDemoStore, persistDemoStore } from './DemoStore'

export class DemoTenantUserRepository implements ITenantUserRepository {
  async listTenantUsers(_tenantId: string): Promise<TenantUserSummary[]> {
    return [...getDemoStore().tenantUsers.value]
  }

  async updateRole(_tenantId: string, userId: string, role: TenantRole): Promise<void> {
    const store = getDemoStore()
    const user = store.tenantUsers.value.find(u => u.userId === userId)
    if (user) {
      user.role = role
      persistDemoStore()
    }
  }

  async setLinkedMember(_tenantId: string, userId: string, memberId: string | null): Promise<void> {
    const store = getDemoStore()
    const user = store.tenantUsers.value.find(u => u.userId === userId)
    if (user) {
      user.linkedMemberId = memberId
      persistDemoStore()
    }
  }

  async listHouseholdUsers(_tenantId: string, householdId: string): Promise<HouseholdUserSummary[]> {
    return getDemoStore().householdUsers.value.filter(hu => hu.householdId === householdId)
  }

  async listUserHouseholdAccess(_tenantId: string, userId: string): Promise<HouseholdUserSummary[]> {
    return getDemoStore().householdUsers.value.filter(hu => hu.userId === userId)
  }

  async upsertHouseholdUser(tenantId: string, householdId: string, userId: string, role: HouseholdRole): Promise<void> {
    const store = getDemoStore()
    const tenantUser = store.tenantUsers.value.find(u => u.userId === userId)
    const externalId = tenantUser?.externalId ?? userId
    const existing = store.householdUsers.value.find(hu => hu.householdId === householdId && hu.userId === userId)
    if (existing) {
      existing.role = role
    }
    else {
      store.householdUsers.value.push({ userId, tenantId, householdId, role, externalId })
    }
    persistDemoStore()
  }

  async deleteHouseholdUser(_tenantId: string, householdId: string, userId: string): Promise<void> {
    const store = getDemoStore()
    store.householdUsers.value = store.householdUsers.value.filter(
      hu => !(hu.householdId === householdId && hu.userId === userId),
    )
    persistDemoStore()
  }
}

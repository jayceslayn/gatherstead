import type { HouseholdRole, HouseholdUserSummary, TenantRole, TenantUserSummary } from '../types'
import type { ITenantUserRepository } from '../interfaces'

export class DemoTenantUserRepository implements ITenantUserRepository {
  async setLinkedMember(_tenantId: string, _userId: string, _memberId: string | null): Promise<void> {}
  async listTenantUsers(_tenantId: string): Promise<TenantUserSummary[]> { return [] }
  async updateRole(_tenantId: string, _userId: string, _role: TenantRole): Promise<void> {}
  async listHouseholdUsers(_tenantId: string, _householdId: string): Promise<HouseholdUserSummary[]> { return [] }
  async upsertHouseholdUser(_tenantId: string, _householdId: string, _userId: string, _role: HouseholdRole): Promise<void> {}
  async deleteHouseholdUser(_tenantId: string, _householdId: string, _userId: string): Promise<void> {}
}

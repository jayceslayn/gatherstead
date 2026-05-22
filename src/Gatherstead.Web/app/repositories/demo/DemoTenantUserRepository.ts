import type { ITenantUserRepository } from '../interfaces'

export class DemoTenantUserRepository implements ITenantUserRepository {
  async setLinkedMember(_tenantId: string, _userId: string, _memberId: string | null): Promise<void> {}
}

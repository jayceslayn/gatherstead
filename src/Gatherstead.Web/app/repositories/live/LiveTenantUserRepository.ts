import type { ITenantUserRepository } from '../interfaces'

export class LiveTenantUserRepository implements ITenantUserRepository {
  async setLinkedMember(tenantId: string, userId: string, memberId: string | null): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/users/${userId}/linked-member`,
      { method: 'PUT', body: { memberId } },
    )
  }
}

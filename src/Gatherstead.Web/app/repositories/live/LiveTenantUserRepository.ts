import type { HouseholdRole, HouseholdUserSummary, TenantRole, TenantUserSummary, InvitationSummary } from '../types'
import type { ITenantUserRepository } from '../interfaces'

export class LiveTenantUserRepository implements ITenantUserRepository {
  async setLinkedMember(tenantId: string, userId: string, memberId: string | null): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/users/${userId}/linked-member`,
      { method: 'PUT', body: { memberId } },
    )
  }

  async listTenantUsers(tenantId: string): Promise<TenantUserSummary[]> {
    const response = await $fetch<{ entity: TenantUserSummary[] }>(
      `/api/proxy/tenants/${tenantId}/users`,
    )
    return response.entity ?? []
  }

  async updateRole(tenantId: string, userId: string, role: TenantRole): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/users/${userId}/role`,
      { method: 'PUT', body: { role } },
    )
  }

  async listUserHouseholdAccess(tenantId: string, userId: string): Promise<HouseholdUserSummary[]> {
    const response = await $fetch<{ entity: HouseholdUserSummary[] }>(
      `/api/proxy/tenants/${tenantId}/users/${userId}/household-access`,
    )
    return response.entity ?? []
  }

  async listHouseholdUsers(tenantId: string, householdId: string): Promise<HouseholdUserSummary[]> {
    const response = await $fetch<{ entity: HouseholdUserSummary[] }>(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/users`,
    )
    return response.entity ?? []
  }

  async upsertHouseholdUser(tenantId: string, householdId: string, userId: string, role: HouseholdRole): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/users/${userId}`,
      { method: 'PUT', body: { role } },
    )
  }

  async deleteHouseholdUser(tenantId: string, householdId: string, userId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/households/${householdId}/users/${userId}`,
      { method: 'DELETE' },
    )
  }

  async inviteUser(
    tenantId: string,
    email: string,
    role: TenantRole,
    householdId?: string | null,
    householdRole?: HouseholdRole | null,
  ): Promise<InvitationSummary> {
    const response = await $fetch<{ entity: InvitationSummary }>(
      `/api/proxy/tenants/${tenantId}/invitations`,
      { method: 'POST', body: { email, role, householdId: householdId ?? null, householdRole: householdRole ?? null } },
    )
    return response.entity
  }

  async listInvitations(tenantId: string): Promise<InvitationSummary[]> {
    const response = await $fetch<{ entity: InvitationSummary[] }>(
      `/api/proxy/tenants/${tenantId}/invitations`,
    )
    return response.entity ?? []
  }

  async revokeInvitation(tenantId: string, invitationId: string): Promise<void> {
    await $fetch(
      `/api/proxy/tenants/${tenantId}/invitations/${invitationId}`,
      { method: 'DELETE' },
    )
  }
}

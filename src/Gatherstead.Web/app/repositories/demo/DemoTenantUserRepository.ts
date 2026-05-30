import type { HouseholdRole, HouseholdUserSummary, TenantRole, TenantUserSummary, InvitationSummary } from '../types'
import type { ITenantUserRepository } from '../interfaces'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

// Demo-only, in-memory invitations. The demo tenant has a single user, so invites stay Pending
// (there is no other matching user to auto-accept) — enough to exercise the invite/list/revoke UX.
const demoInvitations: InvitationSummary[] = []

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

  async inviteUser(
    tenantId: string,
    email: string,
    role: TenantRole,
    householdId?: string | null,
    householdRole?: HouseholdRole | null,
  ): Promise<InvitationSummary> {
    const normalized = email.trim().toLowerCase()
    const existing = demoInvitations.find(i => i.tenantId === tenantId && i.email === normalized && i.status === 'Pending')
    if (existing) return existing

    const invitation: InvitationSummary = {
      id: demoId(),
      tenantId,
      email: normalized,
      role,
      householdId: householdId ?? null,
      householdRole: householdRole ?? null,
      status: 'Pending',
      createdAt: new Date().toISOString(),
      acceptedAt: null,
    }
    demoInvitations.push(invitation)
    return invitation
  }

  async listInvitations(tenantId: string): Promise<InvitationSummary[]> {
    return demoInvitations.filter(i => i.tenantId === tenantId)
  }

  async revokeInvitation(tenantId: string, invitationId: string): Promise<void> {
    const idx = demoInvitations.findIndex(i => i.tenantId === tenantId && i.id === invitationId)
    if (idx >= 0) demoInvitations.splice(idx, 1)
  }
}

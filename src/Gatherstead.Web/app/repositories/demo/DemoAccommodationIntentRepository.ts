import type { IAccommodationIntentRepository } from '../interfaces'
import type {
  AccommodationIntent,
  AccommodationIntentStatus,
  AccommodationIntentDecision,
} from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'

export class DemoAccommodationIntentRepository implements IAccommodationIntentRepository {
  async listIntents(
    tenantId: string,
    _propertyId: string,
    accommodationId: string,
  ): Promise<AccommodationIntent[]> {
    return getDemoStore().accommodationIntents.value.filter(
      i => i.tenantId === tenantId && i.accommodationId === accommodationId,
    )
  }

  async listIntentsForMember(
    tenantId: string,
    _propertyId: string,
    accommodationId: string,
    memberId: string,
  ): Promise<AccommodationIntent[]> {
    return getDemoStore().accommodationIntents.value.filter(
      i =>
        i.tenantId === tenantId
        && i.accommodationId === accommodationId
        && i.householdMemberId === memberId,
    )
  }

  async createIntent(
    tenantId: string,
    _propertyId: string,
    accommodationId: string,
    _householdId: string,
    memberId: string,
    night: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partySize?: number | null,
  ): Promise<AccommodationIntent> {
    const store = getDemoStore()
    const existing = store.accommodationIntents.value.find(
      i => i.accommodationId === accommodationId && i.householdMemberId === memberId && i.night === night,
    )
    if (existing) {
      existing.status = status
      existing.notes = notes ?? null
      existing.partySize = partySize ?? null
      persistDemoStore()
      return existing
    }
    const intent: AccommodationIntent = {
      id: demoId(),
      tenantId,
      accommodationId,
      householdMemberId: memberId,
      night,
      status,
      notes: notes ?? null,
      decision: 'Pending',
      partySize: partySize ?? null,
      priority: null,
    }
    store.accommodationIntents.value.push(intent)
    persistDemoStore()
    return intent
  }

  async updateIntent(
    _tenantId: string,
    _propertyId: string,
    _accommodationId: string,
    intentId: string,
    status: AccommodationIntentStatus,
    decision: AccommodationIntentDecision,
    notes?: string | null,
    partySize?: number | null,
  ): Promise<void> {
    const store = getDemoStore()
    const intent = store.accommodationIntents.value.find(i => i.id === intentId)
    if (!intent) return
    intent.status = status
    intent.decision = decision
    intent.notes = notes ?? null
    intent.partySize = partySize ?? null
    persistDemoStore()
  }

  async deleteIntent(
    _tenantId: string,
    _propertyId: string,
    _accommodationId: string,
    intentId: string,
  ): Promise<void> {
    const store = getDemoStore()
    store.accommodationIntents.value = store.accommodationIntents.value.filter(i => i.id !== intentId)
    persistDemoStore()
  }
}

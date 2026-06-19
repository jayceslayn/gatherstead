import type { IAccommodationIntentRepository } from '../interfaces'
import type {
  AccommodationIntent,
  AccommodationIntentStatus,
  AccommodationIntentDecision,
} from '../types'
import { getDemoStore, persistDemoStore, demoId } from './DemoStore'
import { trackPersistence } from '../../utils/telemetry'

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
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partyAdults?: number | null,
    partyChildren?: number | null,
  ): Promise<AccommodationIntent> {
    const store = getDemoStore()
    // A stay is a span; overlapping stays are allowed (capacity is only a soft UI flag), so each
    // request creates a distinct record.
    const intent: AccommodationIntent = {
      id: demoId(),
      tenantId,
      accommodationId,
      householdMemberId: memberId,
      startNight,
      endNight,
      status,
      notes: notes ?? null,
      decision: 'Pending',
      partyAdults: partyAdults ?? null,
      partyChildren: partyChildren ?? null,
      priority: null,
    }
    store.accommodationIntents.value.push(intent)
    persistDemoStore()
    trackPersistence('accommodation_intent', 'create', { status })
    return intent
  }

  async updateIntent(
    _tenantId: string,
    _propertyId: string,
    _accommodationId: string,
    intentId: string,
    memberId: string,
    targetAccommodationId: string,
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    decision: AccommodationIntentDecision,
    notes?: string | null,
    partyAdults?: number | null,
    partyChildren?: number | null,
  ): Promise<void> {
    const store = getDemoStore()
    const intent = store.accommodationIntents.value.find(i => i.id === intentId)
    if (!intent) return
    intent.householdMemberId = memberId
    intent.accommodationId = targetAccommodationId
    intent.startNight = startNight
    intent.endNight = endNight
    intent.status = status
    intent.decision = decision
    intent.notes = notes ?? null
    intent.partyAdults = partyAdults ?? null
    intent.partyChildren = partyChildren ?? null
    persistDemoStore()
    trackPersistence('accommodation_intent', 'update', { status, decision })  
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
    trackPersistence('accommodation_intent', 'delete')
  }
}

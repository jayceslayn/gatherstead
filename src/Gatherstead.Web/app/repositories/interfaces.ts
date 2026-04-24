import type {
  TenantSummary,
  HouseholdSummary,
  HouseholdMember,
  DietaryProfile,
  EventSummary,
  AttendanceStatus,
  AttendanceRecord,
  MealTemplate,
  MealPlan,
  MealIntent,
  MealIntentStatus,
  ChoreTemplate,
  ChorePlan,
  ChoreIntent,
  PropertySummary,
  AccommodationSummary,
  AccommodationIntent,
  AccommodationIntentStatus,
  AccommodationIntentDecision,
} from './types'

export const REPOSITORIES_KEY = Symbol('repositories')

export class DemoLimitError extends Error {
  constructor(public readonly limitKey: string) {
    super(`Demo limit reached: ${limitKey}`)
    this.name = 'DemoLimitError'
  }
}

export interface ITenantRepository {
  listTenants(): Promise<TenantSummary[]>
}

export interface IHouseholdRepository {
  listHouseholds(tenantId: string): Promise<HouseholdSummary[]>
  getHousehold(tenantId: string, householdId: string): Promise<HouseholdSummary | null>
}

export interface IHouseholdMemberRepository {
  listMembers(tenantId: string, householdId: string): Promise<HouseholdMember[]>
  getMember(tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null>
  getDietaryProfile(tenantId: string, householdId: string, memberId: string): Promise<DietaryProfile | null>
}

export interface IEventRepository {
  listEvents(tenantId: string): Promise<EventSummary[]>
  getEvent(tenantId: string, eventId: string): Promise<EventSummary | null>
}

export interface IEventAttendanceRepository {
  listAttendance(tenantId: string, eventId: string): Promise<AttendanceRecord[]>
  upsertAttendance(
    tenantId: string,
    eventId: string,
    householdId: string,
    memberId: string,
    day: string,
    status: AttendanceStatus,
  ): Promise<void>
}

export interface IMealPlanRepository {
  listMealTemplates(tenantId: string, eventId: string): Promise<MealTemplate[]>
  listPlans(tenantId: string, eventId: string, templateId: string): Promise<MealPlan[]>
  listIntentsForMember(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    memberId: string,
  ): Promise<MealIntent[]>
  upsertIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    householdId: string,
    memberId: string,
    status: MealIntentStatus,
    bringOwnFood: boolean,
  ): Promise<void>
}

export interface IChoreRepository {
  listChoreTemplates(tenantId: string, eventId: string): Promise<ChoreTemplate[]>
  listPlans(tenantId: string, eventId: string, templateId: string): Promise<ChorePlan[]>
  listIntentsForMember(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    memberId: string,
  ): Promise<ChoreIntent[]>
  upsertIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    householdId: string,
    memberId: string,
    volunteered: boolean,
  ): Promise<void>
}

export interface IPropertyRepository {
  listProperties(tenantId: string): Promise<PropertySummary[]>
  getProperty(tenantId: string, propertyId: string): Promise<PropertySummary | null>
}

export interface IAccommodationRepository {
  listAccommodations(tenantId: string, propertyId: string): Promise<AccommodationSummary[]>
  getAccommodation(tenantId: string, propertyId: string, accommodationId: string): Promise<AccommodationSummary | null>
}

export interface IAccommodationIntentRepository {
  listIntents(tenantId: string, propertyId: string, accommodationId: string): Promise<AccommodationIntent[]>
  listIntentsForMember(tenantId: string, propertyId: string, accommodationId: string, memberId: string): Promise<AccommodationIntent[]>
  createIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    householdId: string,
    memberId: string,
    night: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partySize?: number | null,
  ): Promise<AccommodationIntent>
  updateIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    intentId: string,
    status: AccommodationIntentStatus,
    decision: AccommodationIntentDecision,
    notes?: string | null,
    partySize?: number | null,
  ): Promise<void>
}

export interface Repositories {
  tenants: ITenantRepository
  households: IHouseholdRepository
  householdMembers: IHouseholdMemberRepository
  events: IEventRepository
  eventAttendance: IEventAttendanceRepository
  mealPlans: IMealPlanRepository
  chores: IChoreRepository
  properties: IPropertyRepository
  accommodations: IAccommodationRepository
  accommodationIntents: IAccommodationIntentRepository
}

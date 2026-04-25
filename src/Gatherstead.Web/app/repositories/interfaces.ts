import type {
  TenantSummary,
  HouseholdSummary,
  HouseholdMember,
  HouseholdRole,
  DietaryProfile,
  EventSummary,
  AttendanceStatus,
  AttendanceRecord,
  MealTemplate,
  MealPlan,
  MealIntent,
  MealAttendance,
  ChoreTemplate,
  ChorePlan,
  ChoreIntent,
  PropertySummary,
  AccommodationSummary,
  AccommodationType,
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
  createHousehold(tenantId: string, name: string): Promise<HouseholdSummary>
  updateHousehold(tenantId: string, householdId: string, name: string): Promise<void>
  deleteHousehold(tenantId: string, householdId: string): Promise<void>
}

export interface IHouseholdMemberRepository {
  listMembers(tenantId: string, householdId: string): Promise<HouseholdMember[]>
  getMember(tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null>
  getDietaryProfile(tenantId: string, householdId: string, memberId: string): Promise<DietaryProfile | null>
  createMember(
    tenantId: string,
    householdId: string,
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    householdRole: HouseholdRole,
    dietaryNotes: string | null,
    dietaryTags: string[],
  ): Promise<HouseholdMember>
  updateMember(
    tenantId: string,
    householdId: string,
    memberId: string,
    name: string,
    isAdult: boolean,
    ageBand: string | null,
    birthDate: string | null,
    householdRole: HouseholdRole,
    dietaryNotes: string | null,
    dietaryTags: string[],
  ): Promise<void>
  deleteMember(tenantId: string, householdId: string, memberId: string): Promise<void>
}

export interface IEventRepository {
  listEvents(tenantId: string): Promise<EventSummary[]>
  getEvent(tenantId: string, eventId: string): Promise<EventSummary | null>
  createEvent(
    tenantId: string,
    propertyId: string,
    name: string,
    startDate: string,
    endDate: string,
  ): Promise<EventSummary>
  updateEvent(
    tenantId: string,
    eventId: string,
    name: string,
    startDate: string,
    endDate: string,
  ): Promise<void>
  deleteEvent(tenantId: string, eventId: string): Promise<void>
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
  deleteAttendance(tenantId: string, eventId: string, attendanceId: string): Promise<void>
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
    volunteered: boolean,
  ): Promise<void>
  createTemplate(
    tenantId: string,
    eventId: string,
    name: string,
    mealTypes: number,
    notes: string | null,
  ): Promise<MealTemplate>
  updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    mealTypes: number,
    notes: string | null,
  ): Promise<void>
  deleteTemplate(tenantId: string, eventId: string, templateId: string): Promise<void>
  deleteIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    intentId: string,
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
  createTemplate(
    tenantId: string,
    eventId: string,
    name: string,
    timeSlots: number,
    minimumAssignees: number | null,
    notes: string | null,
  ): Promise<ChoreTemplate>
  updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    timeSlots: number,
    minimumAssignees: number | null,
    notes: string | null,
  ): Promise<void>
  deleteTemplate(tenantId: string, eventId: string, templateId: string): Promise<void>
  deleteIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    intentId: string,
  ): Promise<void>
}

export interface IMealAttendanceRepository {
  listMealAttendance(tenantId: string, eventId: string, templateId: string, planId: string): Promise<MealAttendance[]>
  upsertMealAttendance(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    householdId: string,
    memberId: string,
    status: AttendanceStatus,
    bringOwnFood: boolean,
    notes?: string | null,
  ): Promise<MealAttendance>
  deleteMealAttendance(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    attendanceId: string,
  ): Promise<void>
}

export interface IPropertyRepository {
  listProperties(tenantId: string): Promise<PropertySummary[]>
  getProperty(tenantId: string, propertyId: string): Promise<PropertySummary | null>
  createProperty(tenantId: string, name: string): Promise<PropertySummary>
  updateProperty(tenantId: string, propertyId: string, name: string): Promise<void>
  deleteProperty(tenantId: string, propertyId: string): Promise<void>
}

export interface IAccommodationRepository {
  listAccommodations(tenantId: string, propertyId: string): Promise<AccommodationSummary[]>
  getAccommodation(tenantId: string, propertyId: string, accommodationId: string): Promise<AccommodationSummary | null>
  createAccommodation(
    tenantId: string,
    propertyId: string,
    name: string,
    type: AccommodationType,
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
  ): Promise<AccommodationSummary>
  updateAccommodation(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    name: string,
    type: AccommodationType,
    capacityAdults: number | null,
    capacityChildren: number | null,
    notes: string | null,
  ): Promise<void>
  deleteAccommodation(tenantId: string, propertyId: string, accommodationId: string): Promise<void>
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
  deleteIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    intentId: string,
  ): Promise<void>
}

export interface Repositories {
  tenants: ITenantRepository
  households: IHouseholdRepository
  householdMembers: IHouseholdMemberRepository
  events: IEventRepository
  eventAttendance: IEventAttendanceRepository
  mealPlans: IMealPlanRepository
  mealAttendance: IMealAttendanceRepository
  chores: IChoreRepository
  properties: IPropertyRepository
  accommodations: IAccommodationRepository
  accommodationIntents: IAccommodationIntentRepository
}

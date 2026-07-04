import type {
  TenantSummary,
  HouseholdSummary,
  HouseholdMember,
  AgeBandOption,
  DietaryTag,
  EventSummary,
  AttendanceStatus,
  AttendanceRecord,
  MealTemplate,
  MealPlan,
  MealIntent,
  MealAttendance,
  TaskTemplate,
  TaskPlan,
  TaskIntent,
  PropertySummary,
  AccommodationSummary,
  AccommodationType,
  AccommodationIntent,
  AccommodationIntentStatus,
  BedWriteEntry,
  AccommodationAvailability,
  MyStay,
  MyTask,
  TenantRole,
  HouseholdRole,
  TenantUserSummary,
  HouseholdUserSummary,
  EquipmentSummary,
  AttributeWriteEntry,
  EventReport,
  InvitationSummary,
  MeSummary,
  ShoppingItem,
  ShoppingItemIntentStatus,
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
  getTenant(tenantId: string): Promise<TenantSummary | null>
}

export interface IHouseholdRepository {
  listHouseholds(tenantId: string): Promise<HouseholdSummary[]>
  getHousehold(tenantId: string, householdId: string): Promise<HouseholdSummary | null>
  createHousehold(tenantId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<HouseholdSummary>
  updateHousehold(tenantId: string, householdId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<void>
  deleteHousehold(tenantId: string, householdId: string): Promise<void>
}

export interface IAgeBandRepository {
  listAgeBands(): Promise<AgeBandOption[]>
}

export interface IDietaryTagRepository {
  listDietaryTags(): Promise<DietaryTag[]>
}

export interface IHouseholdMemberRepository {
  listMembers(tenantId: string, householdId: string): Promise<HouseholdMember[]>
  getMember(tenantId: string, householdId: string, memberId: string): Promise<HouseholdMember | null>
  createMember(
    tenantId: string,
    householdId: string,
    name: string,
    ageBand: string | null,
    birthDate: string | null,
    dietaryNotes: string | null,
    notes: string | null,
    dietaryTags: string[],
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<HouseholdMember>
  updateMember(
    tenantId: string,
    householdId: string,
    memberId: string,
    name: string,
    ageBand: string | null,
    birthDate: string | null,
    dietaryNotes: string | null,
    notes: string | null,
    dietaryTags: string[],
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void>
  deleteMember(tenantId: string, householdId: string, memberId: string): Promise<void>
}

export interface ITenantUserRepository {
  setLinkedMember(tenantId: string, userId: string, memberId: string | null): Promise<void>
  listTenantUsers(tenantId: string): Promise<TenantUserSummary[]>
  updateRole(tenantId: string, userId: string, role: TenantRole): Promise<void>
  listHouseholdUsers(tenantId: string, householdId: string): Promise<HouseholdUserSummary[]>
  listUserHouseholdAccess(tenantId: string, userId: string): Promise<HouseholdUserSummary[]>
  upsertHouseholdUser(tenantId: string, householdId: string, userId: string, role: HouseholdRole): Promise<void>
  deleteHouseholdUser(tenantId: string, householdId: string, userId: string): Promise<void>
  inviteUser(
    tenantId: string,
    email: string,
    role: TenantRole,
    householdId?: string | null,
    householdRole?: HouseholdRole | null,
  ): Promise<InvitationSummary>
  listInvitations(tenantId: string): Promise<InvitationSummary[]>
  revokeInvitation(tenantId: string, invitationId: string): Promise<void>
}

export interface IMeRepository {
  getMe(): Promise<MeSummary>
  updateDisplayName(displayName: string): Promise<MeSummary>
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
    notes?: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<EventSummary>
  updateEvent(
    tenantId: string,
    eventId: string,
    name: string,
    startDate: string,
    endDate: string,
    notes?: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void>
  deleteEvent(tenantId: string, eventId: string): Promise<void>
}

/** One day-attendance change in a bulk sign-up submission. */
export interface BulkEventAttendanceItem {
  memberId: string
  day: string
  status: AttendanceStatus
}

/** One meal-attendance change in a bulk sign-up submission. */
export interface BulkMealAttendanceItem {
  planId: string
  memberId: string
  status: AttendanceStatus
}

/** One task-intent change in a bulk sign-up submission. Row existence is the sign-up; Source is server-derived. */
export interface BulkTaskIntentItem {
  planId: string
  memberId: string
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
  /** Upserts many day-attendance records in one request. householdId is derived server-side. */
  bulkUpsertAttendance(tenantId: string, eventId: string, items: BulkEventAttendanceItem[]): Promise<void>
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
  ): Promise<void>
  createTemplate(
    tenantId: string,
    eventId: string,
    name: string,
    mealTypes: number,
    startDate: string | null,
    endDate: string | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
    createMatchingTaskTemplate?: boolean,
  ): Promise<MealTemplate>
  updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    mealTypes: number,
    startDate: string | null,
    endDate: string | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void>
  deleteTemplate(tenantId: string, eventId: string, templateId: string): Promise<void>
  updatePlan(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    notes: string | null,
    isException: boolean,
    exceptionReason: string | null,
  ): Promise<void>
  deletePlan(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
  ): Promise<void>
  deleteIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    intentId: string,
  ): Promise<void>
}

export interface ITaskRepository {
  /** A member's signed-up tasks across all events, on or after `fromDay`. */
  listMyTasks(tenantId: string, memberId: string, fromDay: string): Promise<MyTask[]>
  listTaskTemplates(tenantId: string, eventId: string): Promise<TaskTemplate[]>
  listPlans(tenantId: string, eventId: string, templateId: string): Promise<TaskPlan[]>
  listIntentsForMember(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    memberId: string,
  ): Promise<TaskIntent[]>
  listPlanIntents(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
  ): Promise<TaskIntent[]>
  /** Lists all task intents across every plan of the event in one request. */
  listEventIntents(tenantId: string, eventId: string): Promise<TaskIntent[]>
  upsertIntent(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    householdId: string,
    memberId: string,
  ): Promise<void>
  /** Upserts many task intents in one request. householdId is derived server-side. */
  bulkUpsertIntents(tenantId: string, eventId: string, items: BulkTaskIntentItem[]): Promise<void>
  createTemplate(
    tenantId: string,
    eventId: string,
    name: string,
    timeSlots: number,
    startDate: string | null,
    endDate: string | null,
    minimumAssignees: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<TaskTemplate>
  updateTemplate(
    tenantId: string,
    eventId: string,
    templateId: string,
    name: string,
    timeSlots: number,
    startDate: string | null,
    endDate: string | null,
    minimumAssignees: number | null,
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<void>
  deleteTemplate(tenantId: string, eventId: string, templateId: string): Promise<void>
  updatePlan(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    completed: boolean,
    notes: string | null,
    isException: boolean,
    exceptionReason: string | null,
  ): Promise<void>
  deletePlan(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
  ): Promise<void>
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
  /** Lists all meal attendance across every plan of the event in one request. */
  listMealAttendanceForEvent(tenantId: string, eventId: string): Promise<MealAttendance[]>
  /** Upserts many meal-attendance records in one request. householdId is derived server-side. */
  bulkUpsertMealAttendance(tenantId: string, eventId: string, items: BulkMealAttendanceItem[]): Promise<MealAttendance[]>
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
  createProperty(tenantId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<PropertySummary>
  updateProperty(tenantId: string, propertyId: string, name: string, notes?: string | null, attributes?: AttributeWriteEntry[] | null): Promise<void>
  deleteProperty(tenantId: string, propertyId: string): Promise<void>
}

/** Intake for the "Expedia-like" availability search (party size + night span). */
export interface AccommodationAvailabilityQuery {
  startNight: string
  endNight: string
  partyAdults?: number | null
  partyChildren?: number | null
  /** When true, only accommodations that can fit the party are returned; when false, all are
   * returned with a `hasSufficientCapacity` flag. */
  requireCapacity: boolean
}

/** Room/spot dimensions in metres; area override wins over width × depth. Any field may be null. */
export interface AccommodationDimensions {
  widthMeters: number | null
  depthMeters: number | null
  areaSqMeters: number | null
}

export interface IAccommodationRepository {
  listAccommodations(tenantId: string, propertyId: string): Promise<AccommodationSummary[]>
  getAccommodation(tenantId: string, propertyId: string, accommodationId: string): Promise<AccommodationSummary | null>
  /** Tenant-wide availability search across all properties for the given party + dates. */
  searchAvailability(tenantId: string, query: AccommodationAvailabilityQuery): Promise<AccommodationAvailability[]>
  /** A member's stays across all accommodations, ending on or after `fromNight`. */
  listMyStays(tenantId: string, memberId: string, fromNight: string): Promise<MyStay[]>
  createAccommodation(
    tenantId: string,
    propertyId: string,
    name: string,
    type: AccommodationType,
    dimensions: AccommodationDimensions,
    beds: BedWriteEntry[],
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
  ): Promise<AccommodationSummary>
  updateAccommodation(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    name: string,
    type: AccommodationType,
    dimensions: AccommodationDimensions,
    beds: BedWriteEntry[],
    notes: string | null,
    attributes?: AttributeWriteEntry[] | null,
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
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partyAdults?: number | null,
    partyChildren?: number | null,
  ): Promise<AccommodationIntent>
  updateIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    intentId: string,
    memberId: string,
    targetAccommodationId: string,
    startNight: string,
    endNight: string,
    status: AccommodationIntentStatus,
    notes?: string | null,
    partyAdults?: number | null,
    partyChildren?: number | null,
  ): Promise<void>
  deleteIntent(
    tenantId: string,
    propertyId: string,
    accommodationId: string,
    intentId: string,
  ): Promise<void>
}

export interface IEquipmentRepository {
  listEquipment(tenantId: string): Promise<EquipmentSummary[]>
  getEquipment(tenantId: string, equipmentId: string): Promise<EquipmentSummary | null>
  createEquipment(tenantId: string, name: string, propertyId: string | null, notes: string | null, attributes?: AttributeWriteEntry[] | null): Promise<EquipmentSummary>
  updateEquipment(tenantId: string, equipmentId: string, name: string, propertyId: string | null, notes: string | null, attributes?: AttributeWriteEntry[] | null): Promise<void>
  deleteEquipment(tenantId: string, equipmentId: string): Promise<void>
}

export interface IReportRepository {
  getEventMealReport(tenantId: string, eventId: string): Promise<EventReport | null>
}

/** Exactly one scope id identifies a shopping item's origin (property / event / meal plan). */
export interface ShoppingItemScope {
  propertyId?: string | null
  eventId?: string | null
  mealPlanId?: string | null
}

export interface CreateShoppingItemInput extends ShoppingItemScope {
  name: string
  quantityNeeded?: number | null
  unit?: string | null
  neededByDate?: string | null
  category?: string | null
  notes?: string | null
  attributes?: AttributeWriteEntry[] | null
}

export interface UpdateShoppingItemInput {
  name: string
  quantityNeeded?: number | null
  unit?: string | null
  neededByDate?: string | null
  category?: string | null
  notes?: string | null
  attributes?: AttributeWriteEntry[] | null
}

/** One member's contribution toward an item (claim or provide), upserted by member id. */
export interface ShoppingItemIntentInput {
  quantity?: number | null
  status: ShoppingItemIntentStatus
  notes?: string | null
}

export interface IShoppingItemRepository {
  listByEvent(tenantId: string, eventId: string): Promise<ShoppingItem[]>
  listByProperty(tenantId: string, propertyId: string): Promise<ShoppingItem[]>
  /** Items the member has an active Claimed intent on (the "My Upcoming Shopping" widget). */
  listClaimedByMember(tenantId: string, memberId: string): Promise<ShoppingItem[]>
  create(tenantId: string, input: CreateShoppingItemInput): Promise<ShoppingItem>
  updateItem(tenantId: string, itemId: string, input: UpdateShoppingItemInput): Promise<void>
  upsertIntent(tenantId: string, itemId: string, memberId: string, input: ShoppingItemIntentInput): Promise<ShoppingItem>
  removeIntent(tenantId: string, itemId: string, memberId: string): Promise<ShoppingItem>
  deleteItem(tenantId: string, itemId: string): Promise<void>
}

export interface Repositories {
  tenants: ITenantRepository
  households: IHouseholdRepository
  householdMembers: IHouseholdMemberRepository
  ageBands: IAgeBandRepository
  dietaryTags: IDietaryTagRepository
  tenantUsers: ITenantUserRepository
  me: IMeRepository
  events: IEventRepository
  eventAttendance: IEventAttendanceRepository
  mealPlans: IMealPlanRepository
  mealAttendance: IMealAttendanceRepository
  tasks: ITaskRepository
  properties: IPropertyRepository
  accommodations: IAccommodationRepository
  accommodationIntents: IAccommodationIntentRepository
  equipment: IEquipmentRepository
  shoppingItems: IShoppingItemRepository
  reports: IReportRepository
}

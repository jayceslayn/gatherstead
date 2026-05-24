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
  MealAttendance,
  TaskTemplate,
  TaskPlan,
  TaskIntent,
  PropertySummary,
  AccommodationSummary,
  AccommodationType,
  AccommodationIntent,
  AccommodationIntentStatus,
  AccommodationIntentDecision,
  TenantRole,
  HouseholdRole,
  TenantUserSummary,
  HouseholdUserSummary,
  EquipmentSummary,
  TenantAttribute,
  PropertyAttribute,
  AccommodationAttribute,
  HouseholdAttribute,
  EventAttribute,
  MealTemplateAttribute,
  TaskTemplateAttribute,
  EquipmentAttribute,
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
  createHousehold(tenantId: string, name: string, notes?: string | null): Promise<HouseholdSummary>
  updateHousehold(tenantId: string, householdId: string, name: string, notes?: string | null): Promise<void>
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
    dietaryNotes: string | null,
    dietaryTags: string[],
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
  ): Promise<EventSummary>
  updateEvent(
    tenantId: string,
    eventId: string,
    name: string,
    startDate: string,
    endDate: string,
    notes?: string | null,
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
    startDate: string | null,
    endDate: string | null,
    notes: string | null,
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

export interface ITaskRepository {
  listTaskTemplates(tenantId: string, eventId: string): Promise<TaskTemplate[]>
  listPlans(tenantId: string, eventId: string, templateId: string): Promise<TaskPlan[]>
  listIntentsForMember(
    tenantId: string,
    eventId: string,
    templateId: string,
    planId: string,
    memberId: string,
  ): Promise<TaskIntent[]>
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
    startDate: string | null,
    endDate: string | null,
    minimumAssignees: number | null,
    notes: string | null,
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
  createProperty(tenantId: string, name: string, notes?: string | null): Promise<PropertySummary>
  updateProperty(tenantId: string, propertyId: string, name: string, notes?: string | null): Promise<void>
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

export interface IEquipmentRepository {
  listEquipment(tenantId: string): Promise<EquipmentSummary[]>
  getEquipment(tenantId: string, equipmentId: string): Promise<EquipmentSummary | null>
  createEquipment(tenantId: string, name: string, propertyId: string | null, notes: string | null): Promise<EquipmentSummary>
  updateEquipment(tenantId: string, equipmentId: string, name: string, propertyId: string | null, notes: string | null): Promise<void>
  deleteEquipment(tenantId: string, equipmentId: string): Promise<void>
}

export interface ITenantAttributeRepository {
  listAttributes(tenantId: string): Promise<TenantAttribute[]>
  getAttribute(tenantId: string, attributeId: string): Promise<TenantAttribute | null>
  createAttribute(tenantId: string, key: string, value: string, tenantMinRole: number): Promise<TenantAttribute>
  updateAttribute(tenantId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void>
  deleteAttribute(tenantId: string, attributeId: string): Promise<void>
}

export interface IPropertyAttributeRepository {
  listAttributes(tenantId: string, propertyId: string): Promise<PropertyAttribute[]>
  getAttribute(tenantId: string, propertyId: string, attributeId: string): Promise<PropertyAttribute | null>
  createAttribute(tenantId: string, propertyId: string, key: string, value: string, tenantMinRole: number): Promise<PropertyAttribute>
  updateAttribute(tenantId: string, propertyId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void>
  deleteAttribute(tenantId: string, propertyId: string, attributeId: string): Promise<void>
}

export interface IAccommodationAttributeRepository {
  listAttributes(tenantId: string, accommodationId: string): Promise<AccommodationAttribute[]>
  getAttribute(tenantId: string, accommodationId: string, attributeId: string): Promise<AccommodationAttribute | null>
  createAttribute(tenantId: string, accommodationId: string, key: string, value: string, tenantMinRole: number): Promise<AccommodationAttribute>
  updateAttribute(tenantId: string, accommodationId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void>
  deleteAttribute(tenantId: string, accommodationId: string, attributeId: string): Promise<void>
}

export interface IHouseholdAttributeRepository {
  listAttributes(tenantId: string, householdId: string): Promise<HouseholdAttribute[]>
  getAttribute(tenantId: string, householdId: string, attributeId: string): Promise<HouseholdAttribute | null>
  createAttribute(tenantId: string, householdId: string, key: string, value: string, tenantMinRole: number, householdMinRole: number | null): Promise<HouseholdAttribute>
  updateAttribute(tenantId: string, householdId: string, attributeId: string, key: string, value: string, tenantMinRole: number, householdMinRole: number | null): Promise<void>
  deleteAttribute(tenantId: string, householdId: string, attributeId: string): Promise<void>
}

export interface IEventAttributeRepository {
  listAttributes(tenantId: string, eventId: string): Promise<EventAttribute[]>
  getAttribute(tenantId: string, eventId: string, attributeId: string): Promise<EventAttribute | null>
  createAttribute(tenantId: string, eventId: string, key: string, value: string, tenantMinRole: number): Promise<EventAttribute>
  updateAttribute(tenantId: string, eventId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void>
  deleteAttribute(tenantId: string, eventId: string, attributeId: string): Promise<void>
}

export interface IMealTemplateAttributeRepository {
  listAttributes(tenantId: string, mealTemplateId: string): Promise<MealTemplateAttribute[]>
  getAttribute(tenantId: string, mealTemplateId: string, attributeId: string): Promise<MealTemplateAttribute | null>
  createAttribute(tenantId: string, mealTemplateId: string, key: string, value: string, tenantMinRole: number): Promise<MealTemplateAttribute>
  updateAttribute(tenantId: string, mealTemplateId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void>
  deleteAttribute(tenantId: string, mealTemplateId: string, attributeId: string): Promise<void>
}

export interface ITaskTemplateAttributeRepository {
  listAttributes(tenantId: string, taskTemplateId: string): Promise<TaskTemplateAttribute[]>
  getAttribute(tenantId: string, taskTemplateId: string, attributeId: string): Promise<TaskTemplateAttribute | null>
  createAttribute(tenantId: string, taskTemplateId: string, key: string, value: string, tenantMinRole: number): Promise<TaskTemplateAttribute>
  updateAttribute(tenantId: string, taskTemplateId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void>
  deleteAttribute(tenantId: string, taskTemplateId: string, attributeId: string): Promise<void>
}

export interface IEquipmentAttributeRepository {
  listAttributes(tenantId: string, equipmentId: string): Promise<EquipmentAttribute[]>
  getAttribute(tenantId: string, equipmentId: string, attributeId: string): Promise<EquipmentAttribute | null>
  createAttribute(tenantId: string, equipmentId: string, key: string, value: string, tenantMinRole: number): Promise<EquipmentAttribute>
  updateAttribute(tenantId: string, equipmentId: string, attributeId: string, key: string, value: string, tenantMinRole: number): Promise<void>
  deleteAttribute(tenantId: string, equipmentId: string, attributeId: string): Promise<void>
}

export interface Repositories {
  tenants: ITenantRepository
  households: IHouseholdRepository
  householdMembers: IHouseholdMemberRepository
  tenantUsers: ITenantUserRepository
  events: IEventRepository
  eventAttendance: IEventAttendanceRepository
  mealPlans: IMealPlanRepository
  mealAttendance: IMealAttendanceRepository
  tasks: ITaskRepository
  properties: IPropertyRepository
  accommodations: IAccommodationRepository
  accommodationIntents: IAccommodationIntentRepository
  equipment: IEquipmentRepository
  tenantAttributes: ITenantAttributeRepository
  propertyAttributes: IPropertyAttributeRepository
  accommodationAttributes: IAccommodationAttributeRepository
  householdAttributes: IHouseholdAttributeRepository
  eventAttributes: IEventAttributeRepository
  mealTemplateAttributes: IMealTemplateAttributeRepository
  taskTemplateAttributes: ITaskTemplateAttributeRepository
  equipmentAttributes: IEquipmentAttributeRepository
}

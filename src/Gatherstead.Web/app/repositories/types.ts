export type TenantRole = 'Owner' | 'Manager' | 'Coordinator' | 'Member' | 'Guest'

export interface AttributeEntry {
  id: string
  key: string
  value: string
  tenantMinRole: number
  householdMinRole: number | null
}

export interface AttributeWriteEntry {
  key: string
  value: string
  tenantMinRole: number
  householdMinRole?: number | null
}

export interface TenantSummary {
  id: string
  name: string
  userRole: TenantRole | null
  attributes: AttributeEntry[]
}

export interface HouseholdSummary {
  id: string
  tenantId: string
  name: string
  notes: string | null
  attributes: AttributeEntry[]
}

export type HouseholdRole = 'Manager' | 'Member'

export interface TenantUserSummary {
  userId: string
  tenantId: string
  role: TenantRole
  linkedMemberId: string | null
  externalId: string
}

export interface HouseholdUserSummary {
  userId: string
  tenantId: string
  householdId: string
  role: HouseholdRole
  externalId: string
}

export interface HouseholdMember {
  id: string
  tenantId: string
  householdId: string
  name: string
  isAdult: boolean
  ageBand: string | null
  birthDate: string | null
  dietaryNotes: string | null
  dietaryTags: string[]
  attributes: AttributeEntry[]
}

export interface DietaryProfile {
  id: string
  tenantId: string
  householdMemberId: string
  preferredDiet: string
  allergies: string[]
  restrictions: string[]
  notes: string | null
}

export interface EventSummary {
  id: string
  tenantId: string
  propertyId: string
  name: string
  startDate: string
  endDate: string
  notes: string | null
  attributes: AttributeEntry[]
}

export type AttendanceStatus = 'Going' | 'Maybe' | 'NotGoing'

export interface AttendanceRecord {
  id: string
  eventId: string
  householdMemberId: string
  day: string
  status: AttendanceStatus
}

export type MealType = 'Breakfast' | 'Lunch' | 'Dinner'

export interface MealTemplate {
  id: string
  tenantId: string
  eventId: string
  name: string
  mealTypes: number
  startDate: string | null
  endDate: string | null
  notes: string | null
  attributes: AttributeEntry[]
}

export interface MealPlan {
  id: string
  tenantId: string
  mealTemplateId: string
  day: string
  mealType: MealType
  notes: string | null
  isException: boolean
  exceptionReason: string | null
}

export interface MealIntent {
  id: string
  tenantId: string
  mealPlanId: string
  householdMemberId: string
  volunteered: boolean
}

export interface MealAttendance {
  id: string
  tenantId: string
  mealPlanId: string
  householdMemberId: string
  status: AttendanceStatus
  bringOwnFood: boolean
  notes: string | null
}

export type TaskTimeSlot = 'Morning' | 'Midday' | 'Evening' | 'Anytime'

export interface TaskTemplate {
  id: string
  tenantId: string
  eventId: string
  name: string
  timeSlots: number
  startDate: string | null
  endDate: string | null
  minimumAssignees: number | null
  notes: string | null
  attributes: AttributeEntry[]
}

export interface TaskPlan {
  id: string
  tenantId: string
  templateId: string
  day: string
  timeSlot: TaskTimeSlot | null
  completed: boolean
  notes: string | null
  isException: boolean
  exceptionReason: string | null
}

export interface TaskIntent {
  id: string
  tenantId: string
  taskPlanId: string
  householdMemberId: string
  volunteered: boolean
}

export type AccommodationType = 'Bedroom' | 'Bunk' | 'RvPad' | 'Tent' | 'Offsite'
export type AccommodationIntentStatus = 'Intent' | 'Hold' | 'Confirmed'
export type AccommodationIntentDecision = 'Pending' | 'Approved' | 'Declined'

export interface PropertySummary {
  id: string
  tenantId: string
  name: string
  notes: string | null
  attributes: AttributeEntry[]
}

export interface AccommodationSummary {
  id: string
  tenantId: string
  propertyId: string
  name: string
  type: AccommodationType
  capacityAdults: number | null
  capacityChildren: number | null
  notes: string | null
  attributes: AttributeEntry[]
}

export interface AccommodationIntent {
  id: string
  tenantId: string
  accommodationId: string
  householdMemberId: string
  night: string
  status: AccommodationIntentStatus
  notes: string | null
  decision: AccommodationIntentDecision
  partySize: number | null
  priority: number | null
}

export interface EquipmentSummary {
  id: string
  tenantId: string
  propertyId: string | null
  name: string
  notes: string | null
  attributes: AttributeEntry[]
}


export const MEAL_TYPE_FLAGS: Record<MealType, number> = {
  Breakfast: 0x01,
  Lunch: 0x02,
  Dinner: 0x04,
}

export const ALL_MEAL_TYPES: MealType[] = ['Breakfast', 'Lunch', 'Dinner']

export function mealTypesFromFlags(flags: number): MealType[] {
  return ALL_MEAL_TYPES.filter(t => (flags & MEAL_TYPE_FLAGS[t]) !== 0)
}

export const TASK_SLOT_FLAGS: Record<TaskTimeSlot, number> = {
  Morning: 0x01,
  Midday: 0x02,
  Evening: 0x04,
  Anytime: 0x08,
}

export const ALL_TASK_SLOTS: TaskTimeSlot[] = ['Morning', 'Midday', 'Evening', 'Anytime']

export function taskSlotsFromFlags(flags: number): TaskTimeSlot[] {
  return ALL_TASK_SLOTS.filter(s => (flags & TASK_SLOT_FLAGS[s]) !== 0)
}

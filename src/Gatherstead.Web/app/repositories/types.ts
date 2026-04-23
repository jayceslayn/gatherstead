export type TenantRole = 'Owner' | 'Manager' | 'Member' | 'Guest'

export interface TenantSummary {
  id: string
  name: string
  userRole: TenantRole | null
}

export interface HouseholdSummary {
  id: string
  tenantId: string
  name: string
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
export type MealIntentStatus = 'Going' | 'Maybe' | 'NotGoing'

export interface MealTemplate {
  id: string
  tenantId: string
  eventId: string
  name: string
  mealTypes: number
  notes: string | null
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
  status: MealIntentStatus
  bringOwnFood: boolean
  notes: string | null
}

export type ChoreTimeSlot = 'Morning' | 'Midday' | 'Evening' | 'Anytime'

export interface ChoreTemplate {
  id: string
  tenantId: string
  eventId: string
  name: string
  timeSlots: number
  minimumAssignees: number | null
  notes: string | null
}

export interface ChorePlan {
  id: string
  tenantId: string
  templateId: string
  day: string
  timeSlot: ChoreTimeSlot | null
  completed: boolean
  notes: string | null
  isException: boolean
  exceptionReason: string | null
}

export interface ChoreIntent {
  id: string
  tenantId: string
  chorePlanId: string
  householdMemberId: string
  volunteered: boolean
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

export const CHORE_SLOT_FLAGS: Record<ChoreTimeSlot, number> = {
  Morning: 0x01,
  Midday: 0x02,
  Evening: 0x04,
  Anytime: 0x08,
}

export const ALL_CHORE_SLOTS: ChoreTimeSlot[] = ['Morning', 'Midday', 'Evening', 'Anytime']

export function choreSlotsFromFlags(flags: number): ChoreTimeSlot[] {
  return ALL_CHORE_SLOTS.filter(s => (flags & CHORE_SLOT_FLAGS[s]) !== 0)
}

// All data types are derived from the generated API spec — no hand-written definitions.
// Run scripts/generate-openapi.sh to regenerate the source files in ./generated/.
import type { components } from './generated/api'

type S = components['schemas']

// Helper: strips the backend audit block and removes the universal optionality that
// openapi-typescript applies to all DTO fields. Required<T> converts `id?: string` →
// `id: string` and `notes?: string | null` → `notes: string | null`, so IDs and other
// required fields become non-optional while explicitly nullable fields stay nullable.
type OmitAudit<T> = Omit<Required<T>, 'audit'>

// ── Enums ──────────────────────────────────────────────────────────────────
// openapi-typescript inlines enum values per-field rather than naming them.
// Extract each enum type from a DTO that contains it so they stay in sync with the API.
export type TenantRole = NonNullable<S['TenantUserDto']['role']>
export type HouseholdRole = NonNullable<S['HouseholdUserDto']['role']>
export type AttendanceStatus = NonNullable<S['EventAttendanceDto']['status']>
export type InvitationStatus = NonNullable<S['InvitationDto']['status']>
export type AccommodationType = NonNullable<S['AccommodationDto']['type']>
export type AccommodationIntentStatus = NonNullable<S['AccommodationIntentDto']['status']>
export type BedSize = NonNullable<S['BedDto']['size']>
export type IntentSource = NonNullable<S['TaskIntentDto']['source']>
export type DietaryCategory = NonNullable<S['DietaryTagDto']['category']>
export type AgeBand = NonNullable<S['HouseholdMemberDto']['ageBand']>
export type AgeBandOption = S['AgeBandOptionDto']

// MealType and TaskTimeSlot are re-exported from typeUtils (they're also used
// alongside the flag utility functions defined there)
export type { MealType, TaskTimeSlot } from './typeUtils'

// ── Attribute types ────────────────────────────────────────────────────────
// Frontend name 'AttributeEntry' corresponds to the backend 'AttributeDto'.
export type AttributeEntry = S['AttributeDto']
export type AttributeWriteEntry = S['AttributeWriteEntry']

// ── Tenants ────────────────────────────────────────────────────────────────
// TenantSummary augments the backend lightweight record with an attributes array.
// userRole is overridden to TenantRole | null because Swashbuckle 10.x incorrectly
// strips nullable from value-type enums; app admins receive null when they have no
// explicit tenant membership.
export type TenantSummary = Omit<S['TenantSummary'], 'userRole'> & {
  userRole: TenantRole | null
  attributes: S['AttributeDto'][]
}
export type TenantUserSummary = S['TenantUserDto']
export type HouseholdUserSummary = S['HouseholdUserDto']

// ── Invitations ────────────────────────────────────────────────────────────
export type InvitationSummary = S['InvitationDto']
export type InvitationHouseholdGrant = S['InvitationHouseholdGrant']

// ── Current user (the authenticated caller's own profile) ───────────────────
export type MeSummary = S['MeDto']

// ── Households ────────────────────────────────────────────────────────────
export type HouseholdSummary = OmitAudit<S['HouseholdDto']>

// ── HouseholdMembers ───────────────────────────────────────────────────────
// Frontend HouseholdMember omits audit fields. The general 'notes' field is
// surfaced read-only on the member detail page; the API keeps it gated behind
// sensitive-read permission (returns null otherwise).
export type HouseholdMember = OmitAudit<S['HouseholdMemberDto']>

// ── Dietary ───────────────────────────────────────────────────────────────
export type DietaryTag = S['DietaryTagDto']

// ── Properties ────────────────────────────────────────────────────────────
export type PropertySummary = OmitAudit<S['PropertyDto']>

// ── Accommodations ────────────────────────────────────────────────────────
// openapi-typescript marks all DTO fields optional; Required<> restores the non-nullable shape.
export type Bed = Required<S['BedDto']>
export type BedWriteEntry = Required<S['BedWriteEntry']>
export type AccommodationSummary = OmitAudit<S['AccommodationDto']>
export type AccommodationIntent = OmitAudit<S['AccommodationIntentDto']>
export type AccommodationAvailability = OmitAudit<S['AccommodationAvailabilityDto']>
export type MyStay = OmitAudit<S['MyStayDto']>
export type MyTask = OmitAudit<S['MyTaskDto']>

// ── Equipment ─────────────────────────────────────────────────────────────
export type EquipmentSummary = OmitAudit<S['EquipmentDto']>

// ── Events ────────────────────────────────────────────────────────────────
export type EventSummary = OmitAudit<S['EventDto']>
export type AttendanceRecord = Pick<S['EventAttendanceDto'], 'id' | 'eventId' | 'householdMemberId' | 'day' | 'status'>

// ── Meals ─────────────────────────────────────────────────────────────────
export type MealTemplate = OmitAudit<S['MealTemplateDto']>
export type MealPlan = OmitAudit<S['MealPlanDto']>
export type MealAttendance = OmitAudit<S['MealAttendanceDto']>
export type MealIntent = OmitAudit<S['MealIntentDto']>

// ── Tasks ─────────────────────────────────────────────────────────────────
export type TaskTemplate = OmitAudit<S['TaskTemplateDto']>
export type TaskPlan = OmitAudit<S['TaskPlanDto']>
export type TaskIntent = OmitAudit<S['TaskIntentDto']>

// ── Shopping ──────────────────────────────────────────────────────────────
export type ShoppingItem = OmitAudit<S['ShoppingItemDto']>
export type ShoppingItemOrigin = NonNullable<S['ShoppingItemDto']['origin']>
export type ShoppingItemStatus = NonNullable<S['ShoppingItemDto']['status']>
// Matches the element type carried on ShoppingItem.intents (openapi-typescript keeps DTO
// fields optional, so we use the raw DTO rather than OmitAudit's Required<> here).
export type ShoppingItemIntent = S['ShoppingItemIntentDto']
export type ShoppingItemIntentStatus = NonNullable<S['ShoppingItemIntentDto']['status']>

// ── Reports ───────────────────────────────────────────────────────────────
export type EventReport = S['EventReportDto']
export type EventReportDay = S['EventReportDayDto']
export type EventReportDayAttendee = S['EventReportDayAttendeeDto']
export type EventReportMeal = S['EventReportMealDto']
export type EventReportAttendee = S['EventReportAttendeeDto']
export type EventReportDietaryTally = S['DietaryTallyDto']
export type EventReportTask = S['EventReportTaskDto']
export type EventReportAccommodation = S['EventReportAccommodationDto']
export type EventReportOccupant = S['EventReportOccupantDto']

// ── Flag utilities ────────────────────────────────────────────────────────
export {
  MEAL_TYPE_FLAGS,
  ALL_MEAL_TYPES,
  mealTypesFromFlags,
  TASK_SLOT_FLAGS,
  ALL_TASK_SLOTS,
  taskSlotsFromFlags,
  mealTypeFlagsToTaskSlotFlags,
  deriveAgeBand,
} from './typeUtils'

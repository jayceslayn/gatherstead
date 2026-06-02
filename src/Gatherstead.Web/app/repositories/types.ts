// All data types are derived from the generated API spec — no hand-written definitions.
// Run scripts/generate-openapi.sh to regenerate the source files in ./generated/.
import type { components } from './generated/api'

type S = components['schemas']

// Helper: strips backend audit fields that the frontend doesn't render.
// Most entity DTOs include audit fields; frontend types only expose business fields.
type OmitAudit<T> = Omit<T, 'createdAt' | 'updatedAt' | 'isDeleted' | 'deletedAt' | 'deletedByUserId'>

// ── Enums ──────────────────────────────────────────────────────────────────
export type TenantRole = S['TenantRole']
export type HouseholdRole = S['HouseholdRole']
export type AttendanceStatus = S['AttendanceStatus']
export type InvitationStatus = S['InvitationStatus']
export type AccommodationType = S['AccommodationType']
export type AccommodationIntentStatus = S['AccommodationIntentStatus']
export type AccommodationIntentDecision = S['AccommodationIntentDecision']
export type DietaryCategory = S['DietaryCategory']

// MealType and TaskTimeSlot are re-exported from typeUtils (they're also used
// alongside the flag utility functions defined there)
export type { MealType, TaskTimeSlot } from './typeUtils'

// ── Attribute types ────────────────────────────────────────────────────────
// Frontend name 'AttributeEntry' corresponds to the backend 'AttributeDto'.
export type AttributeEntry = S['AttributeDto']
export type AttributeWriteEntry = S['AttributeWriteEntry']

// ── Tenants ────────────────────────────────────────────────────────────────
// TenantSummary augments the backend lightweight record with an attributes array.
// The list endpoint returns the C# TenantSummary (no attributes); the repository
// layer adds attributes: [] to satisfy this unified type.
export type TenantSummary = S['TenantSummary'] & { attributes: S['AttributeDto'][] }
export type TenantUserSummary = S['TenantUserDto']
export type HouseholdUserSummary = S['HouseholdUserDto']

// ── Invitations ────────────────────────────────────────────────────────────
export type InvitationSummary = S['InvitationDto']

// ── Households ────────────────────────────────────────────────────────────
export type HouseholdSummary = OmitAudit<S['HouseholdDto']>

// ── HouseholdMembers ───────────────────────────────────────────────────────
// Frontend HouseholdMember omits the general 'notes' field and audit fields.
export type HouseholdMember = OmitAudit<Omit<S['HouseholdMemberDto'], 'notes'>>

// ── Dietary ───────────────────────────────────────────────────────────────
export type DietaryTag = S['DietaryTagDto']

// ── Properties ────────────────────────────────────────────────────────────
export type PropertySummary = OmitAudit<S['PropertyDto']>

// ── Accommodations ────────────────────────────────────────────────────────
export type AccommodationSummary = OmitAudit<S['AccommodationDto']>
export type AccommodationIntent = OmitAudit<S['AccommodationIntentDto']>

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

// ── Reports ───────────────────────────────────────────────────────────────
export type EventReport = S['EventReportDto']
export type EventReportDay = S['EventReportDayDto']
export type EventReportMeal = S['EventReportMealDto']
export type EventReportAttendee = S['EventReportAttendeeDto']
export type EventReportDietaryTally = S['DietaryTallyDto']

// ── Flag utilities ────────────────────────────────────────────────────────
export {
  MEAL_TYPE_FLAGS,
  ALL_MEAL_TYPES,
  mealTypesFromFlags,
  TASK_SLOT_FLAGS,
  ALL_TASK_SLOTS,
  taskSlotsFromFlags,
  mealTypeFlagsToTaskSlotFlags,
} from './typeUtils'

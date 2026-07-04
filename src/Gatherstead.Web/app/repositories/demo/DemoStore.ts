import { ref } from 'vue'
import type { Ref } from 'vue'
import type {
  TenantSummary,
  HouseholdSummary,
  HouseholdMember,
  HouseholdUserSummary,
  TenantUserSummary,
  EventSummary,
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
  AccommodationIntent,
  EquipmentSummary,
  InvitationSummary,
  ShoppingItem,
} from '../types'
import { DEMO_TENANT, DEMO_USER } from './demoConstants'
import type { DEMO_LIMITS } from './demoConstants'

export {
  DEMO_USER_DISPLAY_NAME,
  DEMO_TENANT_ID,
  DEMO_USER_ID,
  DEMO_USER_EXTERNAL_ID,
  DEMO_TENANT,
  DEMO_USER,
  DEMO_LIMITS,
} from './demoConstants'

// v2: entity-layer rework changed persisted shapes (intent Source, accommodation beds/dimensions,
// member isAdult derivation, accommodation-intent status merge). Bumping the key forces a clean reseed.
const STORAGE_KEY = 'gs-demo-store-v2'

export class DemoLimitError extends Error {
  constructor(public readonly limitKey: keyof typeof DEMO_LIMITS) {
    super(`Demo limit reached: ${limitKey}`)
    this.name = 'DemoLimitError'
  }
}

interface DemoState {
  tenants: TenantSummary[]
  tenantUsers: TenantUserSummary[]
  householdUsers: HouseholdUserSummary[]
  households: HouseholdSummary[]
  members: HouseholdMember[]
  events: EventSummary[]
  attendance: AttendanceRecord[]
  mealTemplates: MealTemplate[]
  mealPlans: MealPlan[]
  mealIntents: MealIntent[]
  mealAttendance: MealAttendance[]
  taskTemplates: TaskTemplate[]
  taskPlans: TaskPlan[]
  taskIntents: TaskIntent[]
  properties: PropertySummary[]
  accommodations: AccommodationSummary[]
  accommodationIntents: AccommodationIntent[]
  equipment: EquipmentSummary[]
  shoppingItems: ShoppingItem[]
  invitations: InvitationSummary[]
}

export interface ReactiveState {
  tenants: Ref<TenantSummary[]>
  tenantUsers: Ref<TenantUserSummary[]>
  householdUsers: Ref<HouseholdUserSummary[]>
  households: Ref<HouseholdSummary[]>
  members: Ref<HouseholdMember[]>
  events: Ref<EventSummary[]>
  attendance: Ref<AttendanceRecord[]>
  mealTemplates: Ref<MealTemplate[]>
  mealPlans: Ref<MealPlan[]>
  mealIntents: Ref<MealIntent[]>
  mealAttendance: Ref<MealAttendance[]>
  taskTemplates: Ref<TaskTemplate[]>
  taskPlans: Ref<TaskPlan[]>
  taskIntents: Ref<TaskIntent[]>
  properties: Ref<PropertySummary[]>
  accommodations: Ref<AccommodationSummary[]>
  accommodationIntents: Ref<AccommodationIntent[]>
  equipment: Ref<EquipmentSummary[]>
  shoppingItems: Ref<ShoppingItem[]>
  invitations: Ref<InvitationSummary[]>
}

function emptyState(): DemoState {
  return {
    tenants: [{ ...DEMO_TENANT }],
    tenantUsers: [{ ...DEMO_USER }],
    householdUsers: [],
    households: [],
    members: [],
    events: [],
    attendance: [],
    mealTemplates: [],
    mealPlans: [],
    mealIntents: [],
    mealAttendance: [],
    taskTemplates: [],
    taskPlans: [],
    taskIntents: [],
    properties: [],
    accommodations: [],
    accommodationIntents: [],
    equipment: [],
    shoppingItems: [],
    invitations: [],
  }
}

function tryParseLocalStorage(): DemoState | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    return JSON.parse(raw) as DemoState
  }
  catch {
    return null
  }
}

function withAttributes<T extends { attributes?: unknown[] }>(items: T[]): (T & { attributes: unknown[] })[] {
  return items.map(item => ({ ...item, attributes: item.attributes ?? [] }))
}

function buildReactiveRefs(state: DemoState): ReactiveState {
  return {
    tenants: ref(withAttributes(state.tenants ?? emptyState().tenants) as TenantSummary[]),
    tenantUsers: ref(state.tenantUsers ?? emptyState().tenantUsers),
    householdUsers: ref(state.householdUsers ?? []),
    households: ref(withAttributes(state.households ?? []) as HouseholdSummary[]),
    members: ref(withAttributes(state.members ?? []) as HouseholdMember[]),
    events: ref(withAttributes(state.events ?? []) as EventSummary[]),
    attendance: ref(state.attendance ?? []),
    mealTemplates: ref(withAttributes(state.mealTemplates ?? []) as MealTemplate[]),
    mealPlans: ref(state.mealPlans ?? []),
    mealIntents: ref(state.mealIntents ?? []),
    mealAttendance: ref(state.mealAttendance ?? []),
    taskTemplates: ref(withAttributes(state.taskTemplates ?? []) as TaskTemplate[]),
    taskPlans: ref(state.taskPlans ?? []),
    taskIntents: ref(state.taskIntents ?? []),
    properties: ref(withAttributes(state.properties ?? []) as PropertySummary[]),
    accommodations: ref(withAttributes(state.accommodations ?? []) as AccommodationSummary[]),
    accommodationIntents: ref(state.accommodationIntents ?? []),
    equipment: ref(withAttributes(state.equipment ?? []) as EquipmentSummary[]),
    shoppingItems: ref(withAttributes(state.shoppingItems ?? []) as ShoppingItem[]),
    invitations: ref(state.invitations ?? []),
  }
}

function snapshot(state: ReactiveState): DemoState {
  return {
    tenants: state.tenants.value,
    tenantUsers: state.tenantUsers.value,
    householdUsers: state.householdUsers.value,
    households: state.households.value,
    members: state.members.value,
    events: state.events.value,
    attendance: state.attendance.value,
    mealTemplates: state.mealTemplates.value,
    mealPlans: state.mealPlans.value,
    mealIntents: state.mealIntents.value,
    mealAttendance: state.mealAttendance.value,
    taskTemplates: state.taskTemplates.value,
    taskPlans: state.taskPlans.value,
    taskIntents: state.taskIntents.value,
    properties: state.properties.value,
    accommodations: state.accommodations.value,
    accommodationIntents: state.accommodationIntents.value,
    equipment: state.equipment.value,
    shoppingItems: state.shoppingItems.value,
    invitations: state.invitations.value,
  }
}

let _state: ReactiveState | null = null

export function getDemoStore(): ReactiveState {
  if (_state) return _state
  const raw = tryParseLocalStorage() ?? emptyState()
  _state = buildReactiveRefs(raw)
  return _state
}

export function persistDemoStore(): void {
  if (!_state) return
  localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot(_state)))
}

export function clearDemoStore(): void {
  if (!_state) return
  _state.tenants.value = [{ ...DEMO_TENANT }]
  _state.tenantUsers.value = emptyState().tenantUsers
  _state.householdUsers.value = []
  _state.households.value = []
  _state.members.value = []
  _state.events.value = []
  _state.attendance.value = []
  _state.mealTemplates.value = []
  _state.mealPlans.value = []
  _state.mealIntents.value = []
  _state.mealAttendance.value = []
  _state.taskTemplates.value = []
  _state.taskPlans.value = []
  _state.taskIntents.value = []
  _state.properties.value = []
  _state.accommodations.value = []
  _state.accommodationIntents.value = []
  _state.equipment.value = []
  _state.shoppingItems.value = []
  _state.invitations.value = []
  localStorage.removeItem(STORAGE_KEY)
}

export function demoId(): string {
  return crypto.randomUUID()
}

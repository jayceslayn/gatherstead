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
} from '../types'

const STORAGE_KEY = 'gs-demo-store'

export const DEMO_USER_DISPLAY_NAME = 'Demo User'
export const DEMO_TENANT_ID = 'demo-tenant'
export const DEMO_USER_ID = 'demo-user'
export const DEMO_USER_EXTERNAL_ID = "demo@example.com"

export const DEMO_TENANT: TenantSummary = {
  id: DEMO_TENANT_ID,
  name: 'The Super Families Network',
  userRole: 'Owner',
}

export const DEMO_USER: TenantUserSummary = {
  userId: DEMO_USER_ID,
  tenantId: DEMO_TENANT_ID,
  role: 'Owner',
  linkedMemberId: null,
  externalId: DEMO_USER_EXTERNAL_ID,
}

export const DEMO_LIMITS = {
  householdsPerTenant: 3,
  membersPerHousehold: 5,
  events: 1,
  eventMaxDays: 3,
  mealTemplatesPerEvent: 3,
  taskTemplatesPerEvent: 4,
  propertiesPerTenant: 2,
  accommodationsPerProperty: 6,
} as const

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

function buildReactiveRefs(state: DemoState): ReactiveState {
  return {
    tenants: ref(state.tenants),
    tenantUsers: ref(state.tenantUsers ?? emptyState().tenantUsers),
    householdUsers: ref(state.householdUsers ?? []),
    households: ref(state.households),
    members: ref(state.members),
    events: ref(state.events),
    attendance: ref(state.attendance),
    mealTemplates: ref(state.mealTemplates),
    mealPlans: ref(state.mealPlans),
    mealIntents: ref(state.mealIntents),
    mealAttendance: ref(state.mealAttendance),
    taskTemplates: ref(state.taskTemplates),
    taskPlans: ref(state.taskPlans),
    taskIntents: ref(state.taskIntents),
    properties: ref(state.properties),
    accommodations: ref(state.accommodations),
    accommodationIntents: ref(state.accommodationIntents),
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
  localStorage.removeItem(STORAGE_KEY)
}

export function demoId(): string {
  return crypto.randomUUID()
}

import { ref } from 'vue'
import type { Ref } from 'vue'
import type {
  TenantSummary,
  HouseholdSummary,
  HouseholdMember,
  EventSummary,
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
  AccommodationIntent,
} from '../types'

const STORAGE_KEY = 'gs-demo-store'

export const DEMO_LIMITS = {
  householdsPerTenant: 3,
  membersPerHousehold: 5,
  events: 1,
  eventMaxDays: 3,
  mealTemplatesPerEvent: 3,
  choreTemplatesPerEvent: 2,
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
  households: HouseholdSummary[]
  members: HouseholdMember[]
  events: EventSummary[]
  attendance: AttendanceRecord[]
  mealTemplates: MealTemplate[]
  mealPlans: MealPlan[]
  mealIntents: MealIntent[]
  mealAttendance: MealAttendance[]
  choreTemplates: ChoreTemplate[]
  chorePlans: ChorePlan[]
  choreIntents: ChoreIntent[]
  properties: PropertySummary[]
  accommodations: AccommodationSummary[]
  accommodationIntents: AccommodationIntent[]
}

export interface ReactiveState {
  tenants: Ref<TenantSummary[]>
  households: Ref<HouseholdSummary[]>
  members: Ref<HouseholdMember[]>
  events: Ref<EventSummary[]>
  attendance: Ref<AttendanceRecord[]>
  mealTemplates: Ref<MealTemplate[]>
  mealPlans: Ref<MealPlan[]>
  mealIntents: Ref<MealIntent[]>
  mealAttendance: Ref<MealAttendance[]>
  choreTemplates: Ref<ChoreTemplate[]>
  chorePlans: Ref<ChorePlan[]>
  choreIntents: Ref<ChoreIntent[]>
  properties: Ref<PropertySummary[]>
  accommodations: Ref<AccommodationSummary[]>
  accommodationIntents: Ref<AccommodationIntent[]>
}

function emptyState(): DemoState {
  return {
    tenants: [
      { id: 'demo-tenant', name: 'The Super Families Network', userRole: 'Owner' },
    ],
    households: [],
    members: [],
    events: [],
    attendance: [],
    mealTemplates: [],
    mealPlans: [],
    mealIntents: [],
    mealAttendance: [],
    choreTemplates: [],
    chorePlans: [],
    choreIntents: [],
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
    households: ref(state.households),
    members: ref(state.members),
    events: ref(state.events),
    attendance: ref(state.attendance),
    mealTemplates: ref(state.mealTemplates),
    mealPlans: ref(state.mealPlans),
    mealIntents: ref(state.mealIntents),
    mealAttendance: ref(state.mealAttendance),
    choreTemplates: ref(state.choreTemplates),
    chorePlans: ref(state.chorePlans),
    choreIntents: ref(state.choreIntents),
    properties: ref(state.properties),
    accommodations: ref(state.accommodations),
    accommodationIntents: ref(state.accommodationIntents),
  }
}

function snapshot(state: ReactiveState): DemoState {
  return {
    tenants: state.tenants.value,
    households: state.households.value,
    members: state.members.value,
    events: state.events.value,
    attendance: state.attendance.value,
    mealTemplates: state.mealTemplates.value,
    mealPlans: state.mealPlans.value,
    mealIntents: state.mealIntents.value,
    mealAttendance: state.mealAttendance.value,
    choreTemplates: state.choreTemplates.value,
    chorePlans: state.chorePlans.value,
    choreIntents: state.choreIntents.value,
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
  const tenant = _state.tenants.value[0]!
  _state.tenants.value = [tenant]
  _state.households.value = []
  _state.members.value = []
  _state.events.value = []
  _state.attendance.value = []
  _state.mealTemplates.value = []
  _state.mealPlans.value = []
  _state.mealIntents.value = []
  _state.mealAttendance.value = []
  _state.choreTemplates.value = []
  _state.chorePlans.value = []
  _state.choreIntents.value = []
  _state.properties.value = []
  _state.accommodations.value = []
  _state.accommodationIntents.value = []
  localStorage.removeItem(STORAGE_KEY)
}

export function demoId(): string {
  return crypto.randomUUID()
}

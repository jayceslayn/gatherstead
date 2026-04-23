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
  ChoreTemplate,
  ChorePlan,
  ChoreIntent,
} from '../types'

const STORAGE_KEY = 'gs-demo-store'

export const DEMO_LIMITS = {
  householdsPerTenant: 3,
  membersPerHousehold: 4,
  events: 1,
  eventMaxDays: 3,
  mealTemplatesPerEvent: 2,
  choreTemplatesPerEvent: 2,
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
  choreTemplates: ChoreTemplate[]
  chorePlans: ChorePlan[]
  choreIntents: ChoreIntent[]
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
  choreTemplates: Ref<ChoreTemplate[]>
  chorePlans: Ref<ChorePlan[]>
  choreIntents: Ref<ChoreIntent[]>
}

function nextWeekendStart(): string {
  const today = new Date()
  const day = today.getDay()
  const daysUntilSat = ((6 - day) + 7) % 7 || 7
  const sat = new Date(today)
  sat.setDate(today.getDate() + daysUntilSat)
  return sat.toISOString().substring(0, 10)
}

function addDays(dateStr: string, n: number): string {
  const d = new Date(dateStr)
  d.setDate(d.getDate() + n)
  return d.toISOString().substring(0, 10)
}

function seedState(): DemoState {
  const eventStart = nextWeekendStart()
  return {
    tenants: [
      { id: 'demo-tenant', name: 'Demo Community', userRole: 'Owner' },
    ],
    households: [
      { id: 'demo-household', tenantId: 'demo-tenant', name: 'The Demo Family' },
    ],
    members: [
      {
        id: 'demo-member',
        tenantId: 'demo-tenant',
        householdId: 'demo-household',
        name: 'Demo User',
        isAdult: true,
        ageBand: null,
        birthDate: null,
        dietaryNotes: null,
        dietaryTags: [],
      },
    ],
    events: [
      {
        id: 'demo-event',
        tenantId: 'demo-tenant',
        propertyId: 'demo-property',
        name: 'Summer Gathering',
        startDate: eventStart,
        endDate: addDays(eventStart, 2),
      },
    ],
    attendance: [],
    mealTemplates: [],
    mealPlans: [],
    mealIntents: [],
    choreTemplates: [],
    chorePlans: [],
    choreIntents: [],
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
    choreTemplates: ref(state.choreTemplates),
    chorePlans: ref(state.chorePlans),
    choreIntents: ref(state.choreIntents),
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
    choreTemplates: state.choreTemplates.value,
    chorePlans: state.chorePlans.value,
    choreIntents: state.choreIntents.value,
  }
}

let _state: ReactiveState | null = null

export function getDemoStore(): ReactiveState {
  if (_state) return _state
  const raw = tryParseLocalStorage() ?? seedState()
  _state = buildReactiveRefs(raw)
  return _state
}

export function persistDemoStore(): void {
  if (!_state) return
  localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot(_state)))
}

export function demoId(): string {
  return 'demo-' + Math.random().toString(36).slice(2, 10)
}

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
  membersPerHousehold: 5,
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
      { id: 'demo-tenant', name: 'The Super Families Network', userRole: 'Owner' },
    ],
    households: [
      { id: 'demo-household-parr', tenantId: 'demo-tenant', name: 'The Parr Family' },
      { id: 'demo-household-frozone', tenantId: 'demo-tenant', name: 'The Frozone Household' },
      { id: 'demo-household-edna', tenantId: 'demo-tenant', name: 'Edna Mode Studio' },
    ],
    members: [
      {
        id: 'demo-member-bob',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-parr',
        name: 'Bob Parr',
        isAdult: true,
        ageBand: null,
        birthDate: null,
        dietaryNotes: 'Large portions — saving the world burns a lot of calories.',
        dietaryTags: [],
      },
      {
        id: 'demo-member-helen',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-parr',
        name: 'Helen Parr',
        isAdult: true,
        ageBand: null,
        birthDate: null,
        dietaryNotes: null,
        dietaryTags: [],
      },
      {
        id: 'demo-member-violet',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-parr',
        name: 'Violet Parr',
        isAdult: false,
        ageBand: '13-17',
        birthDate: null,
        dietaryNotes: 'Will not eat anything if people are watching.',
        dietaryTags: [],
      },
      {
        id: 'demo-member-dash',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-parr',
        name: 'Dash Parr',
        isAdult: false,
        ageBand: '8-12',
        birthDate: null,
        dietaryNotes: 'Eats at top speed. Food must be secured to the plate.',
        dietaryTags: [],
      },
      {
        id: 'demo-member-jackjack',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-parr',
        name: 'Jack-Jack Parr',
        isAdult: false,
        ageBand: '0-3',
        birthDate: null,
        dietaryNotes: 'Baby food only. Keep away from raccoons.',
        dietaryTags: [],
      },
      {
        id: 'demo-member-lucius',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-frozone',
        name: 'Lucius Best',
        isAdult: true,
        ageBand: null,
        birthDate: null,
        dietaryNotes: null,
        dietaryTags: [],
      },
      {
        id: 'demo-member-honey',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-frozone',
        name: 'Honey Best',
        isAdult: true,
        ageBand: null,
        birthDate: null,
        dietaryNotes: null,
        dietaryTags: [],
      },
      {
        id: 'demo-member-edna',
        tenantId: 'demo-tenant',
        householdId: 'demo-household-edna',
        name: 'Edna Mode',
        isAdult: true,
        ageBand: null,
        birthDate: null,
        dietaryNotes: 'No capes. Also no gluten.',
        dietaryTags: ['Gluten-Free'],
      },
    ],
    events: [
      {
        id: 'demo-event',
        tenantId: 'demo-tenant',
        propertyId: 'demo-property',
        name: 'Super Summer Retreat — Keep It Secret!',
        startDate: eventStart,
        endDate: addDays(eventStart, 2),
      },
    ],
    attendance: [],
    mealTemplates: [
      {
        id: 'demo-meal-brunch',
        tenantId: 'demo-tenant',
        eventId: 'demo-event',
        name: "Edna's No-Cape Brunch",
        mealTypes: 0x03, // Breakfast + Lunch
        notes: 'Elegant. Functional. Absolutely no capes on the tablecloth.',
      },
      {
        id: 'demo-meal-bbq',
        tenantId: 'demo-tenant',
        eventId: 'demo-event',
        name: "Bob's Backyard BBQ",
        mealTypes: 0x04, // Dinner
        notes: "Bob's in charge of the grill. Helen's in charge of making sure he doesn't lift it.",
      },
    ],
    mealPlans: [],
    mealIntents: [],
    choreTemplates: [
      {
        id: 'demo-chore-suits',
        tenantId: 'demo-tenant',
        eventId: 'demo-event',
        name: 'Suit Inventory Check',
        timeSlots: 0x01, // Morning
        minimumAssignees: 1,
        notes: 'Coordinate with Edna. Do NOT ask about capes.',
      },
      {
        id: 'demo-chore-dash',
        tenantId: 'demo-tenant',
        eventId: 'demo-event',
        name: 'Keep Dash From Running',
        timeSlots: 0x0f, // Anytime — because it is always a risk
        minimumAssignees: 2,
        notes: 'Two adults minimum. Past attempts with one have failed.',
      },
    ],
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

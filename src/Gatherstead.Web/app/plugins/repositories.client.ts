import { REPOSITORIES_KEY } from '~/repositories/interfaces'
import type { Repositories } from '~/repositories/interfaces'
import { LiveTenantRepository } from '~/repositories/live/LiveTenantRepository'
import { LiveHouseholdRepository } from '~/repositories/live/LiveHouseholdRepository'
import { LiveHouseholdMemberRepository } from '~/repositories/live/LiveHouseholdMemberRepository'
import { LiveEventRepository } from '~/repositories/live/LiveEventRepository'
import { LiveEventAttendanceRepository } from '~/repositories/live/LiveEventAttendanceRepository'
import { LiveMealPlanRepository } from '~/repositories/live/LiveMealPlanRepository'
import { LiveChoreRepository } from '~/repositories/live/LiveChoreRepository'
import { LivePropertyRepository } from '~/repositories/live/LivePropertyRepository'
import { LiveAccommodationRepository } from '~/repositories/live/LiveAccommodationRepository'
import { LiveAccommodationIntentRepository } from '~/repositories/live/LiveAccommodationIntentRepository'
import { DemoTenantRepository } from '~/repositories/demo/DemoTenantRepository'
import { DemoHouseholdRepository } from '~/repositories/demo/DemoHouseholdRepository'
import { DemoHouseholdMemberRepository } from '~/repositories/demo/DemoHouseholdMemberRepository'
import { DemoEventRepository } from '~/repositories/demo/DemoEventRepository'
import { DemoEventAttendanceRepository } from '~/repositories/demo/DemoEventAttendanceRepository'
import { DemoMealPlanRepository } from '~/repositories/demo/DemoMealPlanRepository'
import { DemoChoreRepository } from '~/repositories/demo/DemoChoreRepository'
import { DemoPropertyRepository } from '~/repositories/demo/DemoPropertyRepository'
import { DemoAccommodationRepository } from '~/repositories/demo/DemoAccommodationRepository'
import { DemoAccommodationIntentRepository } from '~/repositories/demo/DemoAccommodationIntentRepository'
import { getDemoStore } from '~/repositories/demo/DemoStore'
import { useCurrentMemberStore } from '~/stores/member'

export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig()

  const repos: Repositories = config.public.demoMode
    ? {
        tenants: new DemoTenantRepository(),
        households: new DemoHouseholdRepository(),
        householdMembers: new DemoHouseholdMemberRepository(),
        events: new DemoEventRepository(),
        eventAttendance: new DemoEventAttendanceRepository(),
        mealPlans: new DemoMealPlanRepository(),
        chores: new DemoChoreRepository(),
        properties: new DemoPropertyRepository(),
        accommodations: new DemoAccommodationRepository(),
        accommodationIntents: new DemoAccommodationIntentRepository(),
      }
    : {
        tenants: new LiveTenantRepository(),
        households: new LiveHouseholdRepository(),
        householdMembers: new LiveHouseholdMemberRepository(),
        events: new LiveEventRepository(),
        eventAttendance: new LiveEventAttendanceRepository(),
        mealPlans: new LiveMealPlanRepository(),
        chores: new LiveChoreRepository(),
        properties: new LivePropertyRepository(),
        accommodations: new LiveAccommodationRepository(),
        accommodationIntents: new LiveAccommodationIntentRepository(),
      }

  nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)

  if (config.public.demoMode) {
    const store = getDemoStore()
    const member = store.members.value[0]
    if (member) {
      const memberStore = useCurrentMemberStore()
      if (!memberStore.linkedMemberId) {
        memberStore.setLinkedMember(member.id, member.householdId)
      }
    }
  }
})

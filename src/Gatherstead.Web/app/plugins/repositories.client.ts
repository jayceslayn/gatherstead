import { REPOSITORIES_KEY } from '~/repositories/interfaces'
import type { Repositories } from '~/repositories/interfaces'
import { LiveTenantRepository } from '~/repositories/live/LiveTenantRepository'
import { LiveHouseholdRepository } from '~/repositories/live/LiveHouseholdRepository'
import { LiveHouseholdMemberRepository } from '~/repositories/live/LiveHouseholdMemberRepository'
import { LiveTenantUserRepository } from '~/repositories/live/LiveTenantUserRepository'
import { LiveEventRepository } from '~/repositories/live/LiveEventRepository'
import { LiveEventAttendanceRepository } from '~/repositories/live/LiveEventAttendanceRepository'
import { LiveMealPlanRepository } from '~/repositories/live/LiveMealPlanRepository'
import { LiveMealAttendanceRepository } from '~/repositories/live/LiveMealAttendanceRepository'
import { LiveTaskRepository } from '~/repositories/live/LiveTaskRepository'
import { LivePropertyRepository } from '~/repositories/live/LivePropertyRepository'
import { LiveAccommodationRepository } from '~/repositories/live/LiveAccommodationRepository'
import { LiveAccommodationIntentRepository } from '~/repositories/live/LiveAccommodationIntentRepository'
import { LiveEquipmentRepository } from '~/repositories/live/LiveEquipmentRepository'
import { LiveReportRepository } from '~/repositories/live/LiveReportRepository'
import { LiveDietaryTagRepository } from '~/repositories/live/LiveDietaryTagRepository'
import { DemoTenantRepository } from '~/repositories/demo/DemoTenantRepository'
import { DemoHouseholdRepository } from '~/repositories/demo/DemoHouseholdRepository'
import { DemoHouseholdMemberRepository } from '~/repositories/demo/DemoHouseholdMemberRepository'
import { DemoTenantUserRepository } from '~/repositories/demo/DemoTenantUserRepository'
import { DemoEventRepository } from '~/repositories/demo/DemoEventRepository'
import { DemoEventAttendanceRepository } from '~/repositories/demo/DemoEventAttendanceRepository'
import { DemoMealPlanRepository } from '~/repositories/demo/DemoMealPlanRepository'
import { DemoMealAttendanceRepository } from '~/repositories/demo/DemoMealAttendanceRepository'
import { DemoTaskRepository } from '~/repositories/demo/DemoTaskRepository'
import { DemoPropertyRepository } from '~/repositories/demo/DemoPropertyRepository'
import { DemoAccommodationRepository } from '~/repositories/demo/DemoAccommodationRepository'
import { DemoAccommodationIntentRepository } from '~/repositories/demo/DemoAccommodationIntentRepository'
import { DemoEquipmentRepository } from '~/repositories/demo/DemoEquipmentRepository'
import { DemoReportRepository } from '~/repositories/demo/DemoReportRepository'
import { DemoDietaryTagRepository } from '~/repositories/demo/DemoDietaryTagRepository'
import { getDemoStore } from '~/repositories/demo/DemoStore'
import { seedDemoData } from '~/repositories/demo/seedDemoData'

export default defineNuxtPlugin(async (nuxtApp) => {
  const config = useRuntimeConfig()

  const repos: Repositories = config.public.demoMode
    ? {
        tenants: new DemoTenantRepository(),
        households: new DemoHouseholdRepository(),
        householdMembers: new DemoHouseholdMemberRepository(),
        dietaryTags: new DemoDietaryTagRepository(),
        tenantUsers: new DemoTenantUserRepository(),
        events: new DemoEventRepository(),
        eventAttendance: new DemoEventAttendanceRepository(),
        mealPlans: new DemoMealPlanRepository(),
        mealAttendance: new DemoMealAttendanceRepository(),
        tasks: new DemoTaskRepository(),
        properties: new DemoPropertyRepository(),
        accommodations: new DemoAccommodationRepository(),
        accommodationIntents: new DemoAccommodationIntentRepository(),
        equipment: new DemoEquipmentRepository(),
        reports: new DemoReportRepository(),
      }
    : {
        tenants: new LiveTenantRepository(),
        households: new LiveHouseholdRepository(),
        householdMembers: new LiveHouseholdMemberRepository(),
        dietaryTags: new LiveDietaryTagRepository(),
        tenantUsers: new LiveTenantUserRepository(),
        events: new LiveEventRepository(),
        eventAttendance: new LiveEventAttendanceRepository(),
        mealPlans: new LiveMealPlanRepository(),
        mealAttendance: new LiveMealAttendanceRepository(),
        tasks: new LiveTaskRepository(),
        properties: new LivePropertyRepository(),
        accommodations: new LiveAccommodationRepository(),
        accommodationIntents: new LiveAccommodationIntentRepository(),
        equipment: new LiveEquipmentRepository(),
        reports: new LiveReportRepository(),
      }

  nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)

  if (config.public.demoMode) {
    const store = getDemoStore()
    if (store.properties.value.length === 0) {
      await seedDemoData(repos)
    }
  }
})

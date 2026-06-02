import { REPOSITORIES_KEY } from '~/repositories/interfaces'
import type { Repositories } from '~/repositories/interfaces'

export default defineNuxtPlugin(async (nuxtApp) => {
  let repos: Repositories

  if (__DEMO_MODE__) {
    const [
      { DemoTenantRepository },
      { DemoHouseholdRepository },
      { DemoHouseholdMemberRepository },
      { DemoDietaryTagRepository },
      { DemoTenantUserRepository },
      { DemoEventRepository },
      { DemoEventAttendanceRepository },
      { DemoMealPlanRepository },
      { DemoMealAttendanceRepository },
      { DemoTaskRepository },
      { DemoPropertyRepository },
      { DemoAccommodationRepository },
      { DemoAccommodationIntentRepository },
      { DemoEquipmentRepository },
      { DemoReportRepository },
      { getDemoStore },
      { seedDemoData },
    ] = await Promise.all([
      import('~/repositories/demo/DemoTenantRepository'),
      import('~/repositories/demo/DemoHouseholdRepository'),
      import('~/repositories/demo/DemoHouseholdMemberRepository'),
      import('~/repositories/demo/DemoDietaryTagRepository'),
      import('~/repositories/demo/DemoTenantUserRepository'),
      import('~/repositories/demo/DemoEventRepository'),
      import('~/repositories/demo/DemoEventAttendanceRepository'),
      import('~/repositories/demo/DemoMealPlanRepository'),
      import('~/repositories/demo/DemoMealAttendanceRepository'),
      import('~/repositories/demo/DemoTaskRepository'),
      import('~/repositories/demo/DemoPropertyRepository'),
      import('~/repositories/demo/DemoAccommodationRepository'),
      import('~/repositories/demo/DemoAccommodationIntentRepository'),
      import('~/repositories/demo/DemoEquipmentRepository'),
      import('~/repositories/demo/DemoReportRepository'),
      import('~/repositories/demo/DemoStore'),
      import('~/repositories/demo/seedDemoData'),
    ])

    repos = {
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

    nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)

    const store = getDemoStore()
    if (store.properties.value.length === 0) {
      await seedDemoData(repos)
    }
  }
  else {
    const [
      { LiveTenantRepository },
      { LiveHouseholdRepository },
      { LiveHouseholdMemberRepository },
      { LiveDietaryTagRepository },
      { LiveTenantUserRepository },
      { LiveEventRepository },
      { LiveEventAttendanceRepository },
      { LiveMealPlanRepository },
      { LiveMealAttendanceRepository },
      { LiveTaskRepository },
      { LivePropertyRepository },
      { LiveAccommodationRepository },
      { LiveAccommodationIntentRepository },
      { LiveEquipmentRepository },
      { LiveReportRepository },
    ] = await Promise.all([
      import('~/repositories/live/LiveTenantRepository'),
      import('~/repositories/live/LiveHouseholdRepository'),
      import('~/repositories/live/LiveHouseholdMemberRepository'),
      import('~/repositories/live/LiveDietaryTagRepository'),
      import('~/repositories/live/LiveTenantUserRepository'),
      import('~/repositories/live/LiveEventRepository'),
      import('~/repositories/live/LiveEventAttendanceRepository'),
      import('~/repositories/live/LiveMealPlanRepository'),
      import('~/repositories/live/LiveMealAttendanceRepository'),
      import('~/repositories/live/LiveTaskRepository'),
      import('~/repositories/live/LivePropertyRepository'),
      import('~/repositories/live/LiveAccommodationRepository'),
      import('~/repositories/live/LiveAccommodationIntentRepository'),
      import('~/repositories/live/LiveEquipmentRepository'),
      import('~/repositories/live/LiveReportRepository'),
    ])

    repos = {
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
  }
})

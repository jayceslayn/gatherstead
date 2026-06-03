import { REPOSITORIES_KEY } from '~/repositories/interfaces'
import type { Repositories } from '~/repositories/interfaces'

export default defineNuxtPlugin(async (nuxtApp) => {
  let repos: Repositories

  if (__DEMO_MODE__) {
    const [demoRepos, { getDemoStore }, { seedDemoData }] = await Promise.all([
      import('~/repositories/demo'),
      import('~/repositories/demo/DemoStore'),
      import('~/repositories/demo/seedDemoData'),
    ])

    repos = {
      tenants: new demoRepos.DemoTenantRepository(),
      households: new demoRepos.DemoHouseholdRepository(),
      householdMembers: new demoRepos.DemoHouseholdMemberRepository(),
      dietaryTags: new demoRepos.DemoDietaryTagRepository(),
      tenantUsers: new demoRepos.DemoTenantUserRepository(),
      events: new demoRepos.DemoEventRepository(),
      eventAttendance: new demoRepos.DemoEventAttendanceRepository(),
      mealPlans: new demoRepos.DemoMealPlanRepository(),
      mealAttendance: new demoRepos.DemoMealAttendanceRepository(),
      tasks: new demoRepos.DemoTaskRepository(),
      properties: new demoRepos.DemoPropertyRepository(),
      accommodations: new demoRepos.DemoAccommodationRepository(),
      accommodationIntents: new demoRepos.DemoAccommodationIntentRepository(),
      equipment: new demoRepos.DemoEquipmentRepository(),
      reports: new demoRepos.DemoReportRepository(),
    }

    nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)

    const store = getDemoStore()
    if (store.properties.value.length === 0) {
      await seedDemoData(repos)
    }
  }
  else {
    const liveRepos = await import('~/repositories/live')

    repos = {
      tenants: new liveRepos.LiveTenantRepository(),
      households: new liveRepos.LiveHouseholdRepository(),
      householdMembers: new liveRepos.LiveHouseholdMemberRepository(),
      dietaryTags: new liveRepos.LiveDietaryTagRepository(),
      tenantUsers: new liveRepos.LiveTenantUserRepository(),
      events: new liveRepos.LiveEventRepository(),
      eventAttendance: new liveRepos.LiveEventAttendanceRepository(),
      mealPlans: new liveRepos.LiveMealPlanRepository(),
      mealAttendance: new liveRepos.LiveMealAttendanceRepository(),
      tasks: new liveRepos.LiveTaskRepository(),
      properties: new liveRepos.LivePropertyRepository(),
      accommodations: new liveRepos.LiveAccommodationRepository(),
      accommodationIntents: new liveRepos.LiveAccommodationIntentRepository(),
      equipment: new liveRepos.LiveEquipmentRepository(),
      reports: new liveRepos.LiveReportRepository(),
    }

    nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)
  }
})

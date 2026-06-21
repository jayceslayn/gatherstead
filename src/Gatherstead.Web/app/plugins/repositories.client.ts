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
      ageBands: new demoRepos.DemoAgeBandRepository(),
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
      shoppingItems: new demoRepos.DemoShoppingItemRepository(),
      reports: new demoRepos.DemoReportRepository(),
    }

    nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)

    // Seed on first load, and re-seed if the persisted demo event has drifted entirely
    // into the past (localStorage survives across sessions but the event dates don't).
    const store = getDemoStore()
    const today = new Date().toISOString().substring(0, 10)
    const stale = store.events.value.length > 0 && store.events.value.every(e => e.endDate < today)
    if (store.properties.value.length === 0 || stale) {
      const { clearDemoStore } = await import('~/repositories/demo/DemoStore')
      clearDemoStore()
      await seedDemoData(repos)
    }
  }
  else {
    const liveRepos = await import('~/repositories/live')

    repos = {
      tenants: new liveRepos.LiveTenantRepository(),
      households: new liveRepos.LiveHouseholdRepository(),
      householdMembers: new liveRepos.LiveHouseholdMemberRepository(),
      ageBands: new liveRepos.LiveAgeBandRepository(),
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
      shoppingItems: new liveRepos.LiveShoppingItemRepository(),
      reports: new liveRepos.LiveReportRepository(),
    }

    nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)
  }
})

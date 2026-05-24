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
import { LiveTenantAttributeRepository } from '~/repositories/live/LiveTenantAttributeRepository'
import { LivePropertyAttributeRepository } from '~/repositories/live/LivePropertyAttributeRepository'
import { LiveAccommodationAttributeRepository } from '~/repositories/live/LiveAccommodationAttributeRepository'
import { LiveHouseholdAttributeRepository } from '~/repositories/live/LiveHouseholdAttributeRepository'
import { LiveEventAttributeRepository } from '~/repositories/live/LiveEventAttributeRepository'
import { LiveMealTemplateAttributeRepository } from '~/repositories/live/LiveMealTemplateAttributeRepository'
import { LiveTaskTemplateAttributeRepository } from '~/repositories/live/LiveTaskTemplateAttributeRepository'
import { LiveEquipmentAttributeRepository } from '~/repositories/live/LiveEquipmentAttributeRepository'
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
import { DemoTenantAttributeRepository } from '~/repositories/demo/DemoTenantAttributeRepository'
import { DemoPropertyAttributeRepository } from '~/repositories/demo/DemoPropertyAttributeRepository'
import { DemoAccommodationAttributeRepository } from '~/repositories/demo/DemoAccommodationAttributeRepository'
import { DemoHouseholdAttributeRepository } from '~/repositories/demo/DemoHouseholdAttributeRepository'
import { DemoEventAttributeRepository } from '~/repositories/demo/DemoEventAttributeRepository'
import { DemoMealTemplateAttributeRepository } from '~/repositories/demo/DemoMealTemplateAttributeRepository'
import { DemoTaskTemplateAttributeRepository } from '~/repositories/demo/DemoTaskTemplateAttributeRepository'
import { DemoEquipmentAttributeRepository } from '~/repositories/demo/DemoEquipmentAttributeRepository'
import { getDemoStore } from '~/repositories/demo/DemoStore'
import { seedDemoData } from '~/repositories/demo/seedDemoData'

export default defineNuxtPlugin(async (nuxtApp) => {
  const config = useRuntimeConfig()

  const repos: Repositories = config.public.demoMode
    ? {
        tenants: new DemoTenantRepository(),
        households: new DemoHouseholdRepository(),
        householdMembers: new DemoHouseholdMemberRepository(),
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
        tenantAttributes: new DemoTenantAttributeRepository(),
        propertyAttributes: new DemoPropertyAttributeRepository(),
        accommodationAttributes: new DemoAccommodationAttributeRepository(),
        householdAttributes: new DemoHouseholdAttributeRepository(),
        eventAttributes: new DemoEventAttributeRepository(),
        mealTemplateAttributes: new DemoMealTemplateAttributeRepository(),
        taskTemplateAttributes: new DemoTaskTemplateAttributeRepository(),
        equipmentAttributes: new DemoEquipmentAttributeRepository(),
      }
    : {
        tenants: new LiveTenantRepository(),
        households: new LiveHouseholdRepository(),
        householdMembers: new LiveHouseholdMemberRepository(),
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
        tenantAttributes: new LiveTenantAttributeRepository(),
        propertyAttributes: new LivePropertyAttributeRepository(),
        accommodationAttributes: new LiveAccommodationAttributeRepository(),
        householdAttributes: new LiveHouseholdAttributeRepository(),
        eventAttributes: new LiveEventAttributeRepository(),
        mealTemplateAttributes: new LiveMealTemplateAttributeRepository(),
        taskTemplateAttributes: new LiveTaskTemplateAttributeRepository(),
        equipmentAttributes: new LiveEquipmentAttributeRepository(),
      }

  nuxtApp.vueApp.provide(REPOSITORIES_KEY, repos)

  if (config.public.demoMode) {
    const store = getDemoStore()
    if (store.properties.value.length === 0) {
      await seedDemoData(repos)
    }
  }
})

import type { TenantSummary, TenantUserSummary } from '../types'

export const DEMO_USER_DISPLAY_NAME = 'Demo User'
export const DEMO_TENANT_ID = 'demo-tenant'
export const DEMO_USER_ID = 'demo-user'
export const DEMO_USER_EXTERNAL_ID = 'demo@example.com'

export const DEMO_TENANT: TenantSummary = {
  id: DEMO_TENANT_ID,
  name: 'The Super Families Network',
  userRole: 'Owner',
  attributes: [],
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
  mealTemplatesPerEvent: 5,
  taskTemplatesPerEvent: 6,
  propertiesPerTenant: 2,
  accommodationsPerProperty: 6,
  equipmentPerTenant: 10,
} as const

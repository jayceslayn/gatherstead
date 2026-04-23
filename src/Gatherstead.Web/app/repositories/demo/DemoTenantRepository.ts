import type { ITenantRepository } from '../interfaces'
import type { TenantSummary } from '../types'
import { getDemoStore } from './DemoStore'

export class DemoTenantRepository implements ITenantRepository {
  async listTenants(): Promise<TenantSummary[]> {
    return [...getDemoStore().tenants.value]
  }
}

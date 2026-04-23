import type { ITenantRepository } from '../interfaces'
import type { TenantSummary, TenantRole } from '../types'

interface TenantsApiResponse {
  entity: Array<{ id: string; name: string; userRole: TenantRole | null }>
  successful: boolean
}

export class LiveTenantRepository implements ITenantRepository {
  async listTenants(): Promise<TenantSummary[]> {
    const r = await $fetch<TenantsApiResponse>('/api/proxy/tenants')
    return (r.entity ?? []).map(t => ({ id: t.id, name: t.name, userRole: t.userRole }))
  }
}

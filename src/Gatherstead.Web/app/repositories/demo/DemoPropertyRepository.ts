import type { IPropertyRepository } from '../interfaces'
import type { PropertySummary } from '../types'
import { getDemoStore } from './DemoStore'

export class DemoPropertyRepository implements IPropertyRepository {
  async listProperties(tenantId: string): Promise<PropertySummary[]> {
    return getDemoStore().properties.value.filter(p => p.tenantId === tenantId)
  }

  async getProperty(tenantId: string, propertyId: string): Promise<PropertySummary | null> {
    return getDemoStore().properties.value.find(
      p => p.tenantId === tenantId && p.id === propertyId,
    ) ?? null
  }
}

import type { MeSummary } from '../types'
import type { IMeRepository } from '../interfaces'

export class LiveMeRepository implements IMeRepository {
  async getMe(): Promise<MeSummary> {
    const response = await $fetch<{ entity: MeSummary }>('/api/proxy/me')
    return response.entity
  }

  async updateDisplayName(displayName: string): Promise<MeSummary> {
    const response = await $fetch<{ entity: MeSummary }>(
      '/api/proxy/me',
      { method: 'PUT', body: { displayName } },
    )
    return response.entity
  }

  async deleteAccount(): Promise<void> {
    await $fetch('/api/proxy/me', { method: 'DELETE' })
  }
}

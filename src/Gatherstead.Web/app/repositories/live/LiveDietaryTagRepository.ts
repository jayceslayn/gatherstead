import type { IDietaryTagRepository } from '../interfaces'
import type { DietaryTag } from '../types'

export class LiveDietaryTagRepository implements IDietaryTagRepository {
  async listDietaryTags(): Promise<DietaryTag[]> {
    const tags = await $fetch<DietaryTag[]>('/api/proxy/dietary-tags')
    return tags ?? []
  }
}

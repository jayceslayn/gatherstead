import type { IAgeBandRepository } from '../interfaces'
import type { AgeBandOption } from '../types'

export class LiveAgeBandRepository implements IAgeBandRepository {
  async listAgeBands(): Promise<AgeBandOption[]> {
    const options = await $fetch<AgeBandOption[]>('/api/proxy/age-bands')
    return options ?? []
  }
}

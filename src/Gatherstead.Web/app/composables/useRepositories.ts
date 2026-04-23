import { inject } from 'vue'
import { REPOSITORIES_KEY } from '~/repositories/interfaces'
import type { Repositories } from '~/repositories/interfaces'

export function useRepositories(): Repositories {
  const repos = inject<Repositories>(REPOSITORIES_KEY)
  if (!repos) throw new Error('[Gatherstead] Repositories not provided.')
  return repos
}

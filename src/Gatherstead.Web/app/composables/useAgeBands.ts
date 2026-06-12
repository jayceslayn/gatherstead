import type { AgeBand, AgeBandOption } from '~/repositories/types'
import { deriveAgeBand } from '~/repositories/types'
import { useRepositories } from '~/composables/useRepositories'

export function useAgeBands() {
  const { ageBands: repo } = useRepositories()

  const { data, pending } = useAsyncData<AgeBandOption[]>(
    'age-bands',
    () => repo.listAgeBands(),
  )

  const options = computed(() => data.value ?? [])

  const optionByValue = computed(() => new Map(options.value.map(o => [o.value, o])))

  function displayName(value: AgeBand | null | undefined): string {
    if (!value) return ''
    return optionByValue.value.get(value)?.displayName ?? value
  }

  // Live preview only — buckets via the API-supplied band ranges, so the boundaries
  // live in exactly one place (the backend). The saved value is re-derived server-side.
  function deriveFromBirthDate(birthDate: string): AgeBand | null {
    return deriveAgeBand(birthDate, options.value)
  }

  const selectItems = computed(() =>
    options.value.map(o => ({ label: o.displayName, value: o.value })),
  )

  return { options, selectItems, displayName, deriveFromBirthDate, pending }
}

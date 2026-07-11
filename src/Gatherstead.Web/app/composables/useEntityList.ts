import type { AsyncDataOptions } from '#app'

/**
 * Read-side wrapper for the standard list-fetch shape: `useAsyncData` keyed per tenant/params,
 * with `items` falling back to `[]` while loading and an optional stable `sort` applied on top.
 * Callers rename `items` on destructure (`const { items: events, ... }`).
 */
export function useEntityList<T>(
  key: () => string,
  fetcher: () => Promise<T[]>,
  options?: {
    watch?: AsyncDataOptions<T[]>['watch']
    sort?: (a: T, b: T) => number
  },
) {
  const { data, pending, error, refresh } = useAsyncData<T[]>(key, fetcher, { watch: options?.watch })

  const items = computed(() => {
    const list = data.value ?? []
    return options?.sort ? [...list].sort(options.sort) : list
  })

  return { items, pending, error, refresh }
}

/**
 * Reactive membership set for expand/collapse-by-key UI (report lanes, grid rows).
 * `toggle` replaces the Set instance so computed consumers re-evaluate.
 */
export function useToggleSet() {
  const set = ref<Set<string>>(new Set())

  function toggle(key: string) {
    const next = new Set(set.value)
    if (next.has(key)) next.delete(key)
    else next.add(key)
    set.value = next
  }

  function has(key: string): boolean {
    return set.value.has(key)
  }

  function clear() {
    set.value = new Set()
  }

  return { set, toggle, has, clear }
}

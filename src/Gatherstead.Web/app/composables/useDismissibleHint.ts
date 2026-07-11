/**
 * Versioned dismissal state for first-time usability hints, persisted in
 * localStorage under `storageKey`. The stored value is the version that was
 * dismissed; bumping `version` at the call site re-shows the hint to users
 * who dismissed an older one. State is shared per key via `useState` so
 * multiple instances (e.g. the same hint on two tabs) stay in sync.
 */
export function useDismissibleHint(storageKey: string, version = 1) {
  const visible = useState(`dismissible-hint:${storageKey}`, () => false)

  onMounted(() => {
    visible.value = Number(localStorage.getItem(storageKey) ?? 0) < version
  })

  function dismiss() {
    visible.value = false
    localStorage.setItem(storageKey, String(version))
  }

  function show() {
    visible.value = true
    localStorage.removeItem(storageKey)
  }

  return { visible, dismiss, show }
}

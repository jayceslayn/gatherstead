import { describe, expect, it, vi, beforeEach } from 'vitest'
import { ref } from 'vue'

// useTrackedAction relies on Nuxt auto-imports (ref, useToast, useI18n, useApiError);
// stub them the same way useApiError.spec.ts does, with vue's real ref for reactivity.
const toasts: Array<{ title: string, description?: string, color: string }> = []
vi.stubGlobal('ref', ref)
vi.stubGlobal('useToast', () => ({ add: (toast: (typeof toasts)[number]) => toasts.push(toast) }))
vi.stubGlobal('useI18n', () => ({ t: (key: string) => key }))
vi.stubGlobal('useApiError', () => ({ translateError: () => 'translated error' }))

const { useTrackedAction } = await import('../../app/composables/useTrackedAction')
const { DemoLimitError } = await import('../../app/repositories/interfaces')

beforeEach(() => {
  toasts.length = 0
})

describe('useTrackedAction', () => {
  it('tracks the key while running, refreshes on success, and resolves true', async () => {
    const refresh = vi.fn().mockResolvedValue(undefined)
    const { updating, run } = useTrackedAction(refresh)

    let keyDuringRun: string[] = []
    const ok = await run('abc', async () => {
      keyDuringRun = [...updating.value]
    })

    expect(ok).toBe(true)
    expect(keyDuringRun).toEqual(['abc'])
    expect(updating.value).toEqual([])
    expect(refresh).toHaveBeenCalledOnce()
    expect(toasts).toEqual([])
  })

  it('shows the demo-limit warning toast on DemoLimitError, skipping refresh', async () => {
    const refresh = vi.fn()
    const { updating, run } = useTrackedAction(refresh)

    const ok = await run('new', () => Promise.reject(new DemoLimitError('events')))

    expect(ok).toBe(false)
    expect(updating.value).toEqual([])
    expect(refresh).not.toHaveBeenCalled()
    expect(toasts).toEqual([{
      title: 'demo.limitReached.title',
      description: 'demo.limitReached.description',
      color: 'warning',
    }])
  })

  it('shows a translated error toast on any other failure', async () => {
    const { updating, run } = useTrackedAction()

    const ok = await run('abc', () => Promise.reject(new Error('boom')))

    expect(ok).toBe(false)
    expect(updating.value).toEqual([])
    expect(toasts).toEqual([{ title: 'translated error', color: 'error' }])
  })

  it('only clears its own key when actions overlap', async () => {
    const { updating, run } = useTrackedAction()

    let release!: () => void
    const gate = new Promise<void>((resolve) => { release = resolve })
    const slow = run('slow', () => gate)
    const fast = await run('fast', () => Promise.resolve())

    expect(fast).toBe(true)
    expect(updating.value).toEqual(['slow'])
    release()
    await slow
    expect(updating.value).toEqual([])
  })
})

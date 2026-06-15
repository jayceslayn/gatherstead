import type { ComputedRef, InjectionKey, Ref } from 'vue'

/**
 * Shared geometry + pager state that `GsSwimlaneGroup` provides to its child
 * `GsSwimlane` lanes, so every lane aligns to the same day columns and reacts
 * to the same mobile selected-day without prop-drilling.
 */
export interface SwimlaneContext {
  days: ComputedRef<string[]>
  gridStyle: ComputedRef<{ gridTemplateColumns: string }>
  selectedDayIndex: Ref<number>
  selectedDay: ComputedRef<string | undefined>
}

export const swimlaneKey: InjectionKey<SwimlaneContext> = Symbol('gsSwimlane')

export function useSwimlaneContext(): SwimlaneContext {
  const ctx = inject(swimlaneKey)
  if (!ctx) throw new Error('GsSwimlane must be used inside a GsSwimlaneGroup')
  return ctx
}

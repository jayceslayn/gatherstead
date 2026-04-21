import { defineStore } from 'pinia'

export const useEventStore = defineStore('event', () => {
  const activeEventId = ref<string | null>(null)
  const activeEventStart = ref<string | null>(null)
  const activeEventEnd = ref<string | null>(null)

  function setActiveEvent(id: string, start: string, end: string) {
    activeEventId.value = id
    activeEventStart.value = start
    activeEventEnd.value = end
  }

  function clear() {
    activeEventId.value = null
    activeEventStart.value = null
    activeEventEnd.value = null
  }

  return { activeEventId, activeEventStart, activeEventEnd, setActiveEvent, clear }
})

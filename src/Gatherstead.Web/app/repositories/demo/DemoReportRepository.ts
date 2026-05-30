import type { IReportRepository } from '../interfaces'
import type {
  EventReport,
  EventReportDay,
  EventReportMeal,
  EventReportAttendee,
  EventReportDietaryTally,
  MealType,
} from '../types'
import { getDemoStore } from './DemoStore'
import { enumDays } from './DemoHelpers'

const MEAL_TYPE_ORDER: Record<MealType, number> = { Breakfast: 0, Lunch: 1, Dinner: 2 }

export class DemoReportRepository implements IReportRepository {
  // Mirrors the backend EventReportService aggregation against the in-memory demo store.
  // Demo dietary signals come from member.dietaryTags (no separate dietary profile store).
  async getEventMealReport(_tenantId: string, eventId: string): Promise<EventReport | null> {
    const store = getDemoStore()
    const event = store.events.value.find(e => e.id === eventId)
    if (!event) return null

    const memberById = new Map(store.members.value.map(m => [m.id, m]))
    const templateNameById = new Map(store.mealTemplates.value.map(t => [t.id, t.name]))
    const planIds = new Set(
      store.mealPlans.value
        .filter(p => templateNameById.has(p.mealTemplateId))
        .map(p => p.id),
    )

    // De-duplicate case-insensitively to match the backend's StringComparer.OrdinalIgnoreCase,
    // keeping the first-seen casing for display.
    const dietaryForMember = (memberId: string): string[] => {
      const m = memberById.get(memberId)
      if (!m) return []
      const seen = new Map<string, string>()
      for (const tag of m.dietaryTags.map(t => t.trim()).filter(Boolean)) {
        const key = tag.toLowerCase()
        if (!seen.has(key)) seen.set(key, tag)
      }
      return [...seen.values()]
    }

    const days: EventReportDay[] = []
    for (const day of enumDays(event.startDate, event.endDate)) {
      const dayEventAttendance = store.attendance.value.filter(a => a.eventId === eventId && a.day === day)
      const going = dayEventAttendance.filter(a => a.status === 'Going').length
      const maybe = dayEventAttendance.filter(a => a.status === 'Maybe').length

      const dayPlans = store.mealPlans.value
        .filter(p => templateNameById.has(p.mealTemplateId) && p.day === day)
        .sort((a, b) => MEAL_TYPE_ORDER[a.mealType] - MEAL_TYPE_ORDER[b.mealType])

      const meals: EventReportMeal[] = dayPlans.map((plan) => {
        const planAttendance = store.mealAttendance.value.filter(a => planIds.has(a.mealPlanId) && a.mealPlanId === plan.id)

        const attendees: EventReportAttendee[] = planAttendance
          .filter(a => a.status !== 'NotGoing')
          .map(a => ({
            memberId: a.householdMemberId,
            name: memberById.get(a.householdMemberId)?.name ?? '',
            status: a.status,
            bringOwnFood: a.bringOwnFood,
            dietary: dietaryForMember(a.householdMemberId),
          }))
          .sort((x, y) => x.name.localeCompare(y.name))

        // Tally case-insensitively (matching the backend); keep first-seen casing for the label.
        const tallyMap = new Map<string, { label: string, count: number }>()
        for (const att of attendees) {
          for (const label of att.dietary) {
            const key = label.toLowerCase()
            const existing = tallyMap.get(key)
            if (existing) existing.count += 1
            else tallyMap.set(key, { label, count: 1 })
          }
        }
        const dietary: EventReportDietaryTally[] = [...tallyMap.values()]
          .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label))

        return {
          mealPlanId: plan.id,
          templateName: templateNameById.get(plan.mealTemplateId) ?? '',
          mealType: plan.mealType,
          going: planAttendance.filter(a => a.status === 'Going').length,
          maybe: planAttendance.filter(a => a.status === 'Maybe').length,
          notGoing: planAttendance.filter(a => a.status === 'NotGoing').length,
          bringOwnFood: planAttendance.filter(a => a.status !== 'NotGoing' && a.bringOwnFood).length,
          dietary,
          attendees,
        }
      })

      days.push({ day, going, maybe, meals })
    }

    return {
      eventId: event.id,
      eventName: event.name,
      startDate: event.startDate,
      endDate: event.endDate,
      days,
    }
  }
}

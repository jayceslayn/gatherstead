import type { IReportRepository } from '../interfaces'
import type {
  EventReport,
  EventReportDay,
  EventReportMeal,
  EventReportAttendee,
  EventReportDietaryTally,
  EventReportTask,
  EventReportAccommodation,
  EventReportOccupant,
  MealType,
} from '../types'
import { getDemoStore } from './DemoStore'
import { enumDays } from './DemoHelpers'
import { DEMO_DIETARY_TAGS } from './DemoDietaryTagRepository'

const SLUG_TO_DISPLAY_NAME = new Map(DEMO_DIETARY_TAGS.map(t => [t.slug.toLowerCase(), t.displayName]))

const MEAL_TYPE_ORDER: Record<MealType, number> = { Breakfast: 0, Lunch: 1, Dinner: 2 }

// "Anytime" (and unslotted) tasks lead, then the timed slots in order (mirrors the backend sort).
const TASK_SLOT_ORDER: Record<string, number> = { Anytime: -1, Morning: 0, Midday: 1, Evening: 2 }
function taskSlotRank(slot: string | null | undefined): number {
  return slot ? TASK_SLOT_ORDER[slot] ?? 99 : -1
}

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

    // Task templates → plans → intents for this event.
    const taskTemplateById = new Map(
      store.taskTemplates.value.filter(t => t.eventId === eventId).map(t => [t.id, t]),
    )
    const taskPlansForEvent = store.taskPlans.value.filter(p => taskTemplateById.has(p.templateId))
    const intentsByPlan = new Map<string, string[]>()
    for (const intent of store.taskIntents.value) {
      const names = intentsByPlan.get(intent.taskPlanId) ?? []
      names.push(memberById.get(intent.householdMemberId)?.name ?? '')
      intentsByPlan.set(intent.taskPlanId, names)
    }

    // Accommodations for the event's property → intents on the event's nights.
    const eventAccommodations = store.accommodations.value.filter(a => a.propertyId === event.propertyId)

    // Resolve slugs to display names (mirrors backend slug → DisplayName lookup).
    // Unknown slugs fall back to the slug string itself.
    const dietaryForMember = (memberId: string): string[] => {
      const m = memberById.get(memberId)
      if (!m) return []
      const seen = new Map<string, string>()
      for (const slug of m.dietaryTags.map(t => t.trim()).filter(Boolean)) {
        const key = slug.toLowerCase()
        if (!seen.has(key)) seen.set(key, SLUG_TO_DISPLAY_NAME.get(key) ?? slug)
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
            dietaryNotes: memberById.get(a.householdMemberId)?.dietaryNotes ?? null,
          }))
          .sort((x, y) => x.name.localeCompare(y.name))

        // Group attendees by their full sorted tag combo (mirrors backend per-member combo tally).
        const comboMap = new Map<string, number>()
        for (const att of attendees) {
          const combo = att.dietary
            .map(d => d.trim()).filter(Boolean)
            .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
            .join(', ')
          comboMap.set(combo, (comboMap.get(combo) ?? 0) + 1)
        }
        const dietary: EventReportDietaryTally[] = [...comboMap.entries()]
          .map(([key, count]) => ({ label: key === '' ? 'No dietary restrictions' : key, count }))
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

      // Tasks for this day, ordered by slot then template name (mirrors backend).
      const tasks: EventReportTask[] = taskPlansForEvent
        .filter(p => p.day === day)
        .map((plan): EventReportTask => {
          const template = taskTemplateById.get(plan.templateId)
          const assignees = [...(intentsByPlan.get(plan.id) ?? [])]
            .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
          return {
            taskPlanId: plan.id,
            templateId: plan.templateId,
            templateName: template?.name ?? '',
            timeSlot: plan.timeSlot ?? null,
            assigneeCount: assignees.length,
            minimumAssignees: template?.minimumAssignees ?? null,
            completed: plan.completed ?? false,
            isException: plan.isException ?? false,
            exceptionReason: plan.exceptionReason ?? null,
            assignees,
          }
        })
        .sort((a, b) =>
          taskSlotRank(a.timeSlot) - taskSlotRank(b.timeSlot)
          || a.templateName.localeCompare(b.templateName, undefined, { sensitivity: 'base' }))

      // Accommodations occupied this night (declined excluded; null party size counts as 1).
      const accommodations: EventReportAccommodation[] = eventAccommodations
        .map((accommodation): EventReportAccommodation | null => {
          const occupants: EventReportOccupant[] = store.accommodationIntents.value
            .filter(i => i.accommodationId === accommodation.id && i.night === day && i.decision !== 'Declined')
            .map((i): EventReportOccupant => ({
              memberId: i.householdMemberId,
              name: memberById.get(i.householdMemberId)?.name ?? '',
              status: i.status ?? 'Intent',
              decision: i.decision ?? 'Pending',
              partySize: i.partySize ?? null,
            }))
            .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }))

          if (occupants.length === 0) return null

          return {
            accommodationId: accommodation.id,
            name: accommodation.name,
            type: accommodation.type ?? 'Bedroom',
            capacityAdults: accommodation.capacityAdults ?? null,
            capacityChildren: accommodation.capacityChildren ?? null,
            occupied: occupants.reduce((sum, o) => sum + (o.partySize ?? 1), 0),
            occupants,
          }
        })
        .filter((a): a is EventReportAccommodation => a !== null)
        .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }))

      days.push({ day, going, maybe, meals, tasks, accommodations })
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

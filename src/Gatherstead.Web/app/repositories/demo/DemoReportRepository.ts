import type { IReportRepository } from '../interfaces'
import type {
  EventReport,
  EventReportDay,
  EventReportDayAttendee,
  EventReportMeal,
  EventReportAttendee,
  EventReportDietaryTally,
  EventReportTask,
  EventReportAccommodation,
  EventReportOccupant,
} from '../types'
import { getDemoStore } from './DemoStore'
import { enumDays, sleepsCapacity } from './DemoHelpers'
import { DEMO_DIETARY_TAGS } from './DemoDietaryTagRepository'
import { DEMO_AGE_BANDS } from './DemoAgeBandRepository'
import { compareOrderKeys, mealSlotRank, planAggregate, taskSlotRank } from '../../composables/useTemplateOrder'
import { byName, compareAccommodations, compareMembers } from '../../utils/sorting'
import { deriveAgeBand } from '../types'

const SLUG_TO_DISPLAY_NAME = new Map(DEMO_DIETARY_TAGS.map(t => [t.slug.toLowerCase(), t.displayName]))

// Groups plans by a (slot, template) key and reduces each group to its ordering aggregate.
function aggregateByKey<T extends { day: string, isException?: boolean | null }>(
  plans: T[], keyOf: (p: T) => string,
): Map<string, { firstEffectiveDay: string, effectiveCount: number }> {
  const groups = new Map<string, T[]>()
  for (const p of plans) {
    const arr = groups.get(keyOf(p))
    if (arr) arr.push(p)
    else groups.set(keyOf(p), [p])
  }
  return new Map([...groups].map(([k, g]) => [k, planAggregate(g)]))
}

export class DemoReportRepository implements IReportRepository {
  // Mirrors the backend EventReportService aggregation against the in-memory demo store.
  // Demo dietary signals come from member.dietaryTags (no separate dietary profile store).
  async getEventMealReport(_tenantId: string, eventId: string): Promise<EventReport | null> {
    const store = getDemoStore()
    const event = store.events.value.find(e => e.id === eventId)
    if (!event) return null

    // Use the effective age band (derived from birth date, else the stored manual band) so members
    // sort by real age — mirrors the list repo's withDerivedAgeBand and the backend's MapToDto. The
    // store keeps the raw band (null when a birth date is set), so compareMembers would otherwise
    // treat birth-dated members as unknown-band and sort them last.
    const memberById = new Map(store.members.value.map(m =>
      [m.id, { ...m, ageBand: m.birthDate ? deriveAgeBand(m.birthDate, DEMO_AGE_BANDS) : m.ageBand }]))
    // Household name per member, so occupant/attendee lists group same-household people together
    // (household name, then member name) — mirrors the backend EventReportService ordering.
    const householdNameById = new Map(store.households.value.map(h => [h.id, h.name]))
    const householdNameForMember = (memberId: string): string =>
      householdNameById.get(memberById.get(memberId)?.householdId ?? '') ?? ''
    // Oldest-first member ordering (age band desc, then birth date, then name) — mirrors the
    // backend EventReportService.MemberSortKey via the shared compareMembers.
    const compareMembersById = (aId: string, bId: string): number => {
      const a = memberById.get(aId)
      const b = memberById.get(bId)
      return compareMembers(
        { ageBand: a?.ageBand ?? null, birthDate: a?.birthDate ?? null, name: a?.name ?? '' },
        { ageBand: b?.ageBand ?? null, birthDate: b?.birthDate ?? null, name: b?.name ?? '' },
      )
    }
    const templateNameById = new Map(store.mealTemplates.value.map(t => [t.id, t.name]))
    const eventMealPlans = store.mealPlans.value.filter(p => templateNameById.has(p.mealTemplateId))
    const planIds = new Set(eventMealPlans.map(p => p.id))

    // Task templates → plans → intents for this event.
    const taskTemplateById = new Map(
      store.taskTemplates.value.filter(t => t.eventId === eventId).map(t => [t.id, t]),
    )
    const taskPlansForEvent = store.taskPlans.value.filter(p => taskTemplateById.has(p.templateId))

    // Per (slot, template) ordering aggregates so days order templates by the shared scheme.
    const mealOrderKey = aggregateByKey(eventMealPlans, p => `${p.mealType}:${p.mealTemplateId}`)
    const taskOrderKey = aggregateByKey(taskPlansForEvent, p => `${p.templateId}:${p.timeSlot ?? ''}`)
    const intentsByPlan = new Map<string, string[]>() // taskPlanId → householdMemberId[]
    for (const intent of store.taskIntents.value) {
      const ids = intentsByPlan.get(intent.taskPlanId) ?? []
      ids.push(intent.householdMemberId)
      intentsByPlan.set(intent.taskPlanId, ids)
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

      // Every response for that day — including NotGoing — grouped by household then
      // oldest-first member order (mirrors backend ordering).
      const dayAttendees: EventReportDayAttendee[] = dayEventAttendance
        .map((a): EventReportDayAttendee => ({
          memberId: a.householdMemberId,
          name: memberById.get(a.householdMemberId)?.name ?? '',
          status: a.status,
          householdId: memberById.get(a.householdMemberId)?.householdId ?? '',
          householdName: householdNameForMember(a.householdMemberId),
        }))
        .sort((x, y) => byName(x.householdName, y.householdName) || compareMembersById(x.memberId, y.memberId))

      const dayPlans = eventMealPlans
        .filter(p => p.day === day)
        .sort((a, b) => compareOrderKeys(
          { slotRank: mealSlotRank(a.mealType), ...mealOrderKey.get(`${a.mealType}:${a.mealTemplateId}`)!, title: templateNameById.get(a.mealTemplateId) ?? '' },
          { slotRank: mealSlotRank(b.mealType), ...mealOrderKey.get(`${b.mealType}:${b.mealTemplateId}`)!, title: templateNameById.get(b.mealTemplateId) ?? '' },
        ))

      const meals: EventReportMeal[] = dayPlans.map((plan) => {
        const planAttendance = store.mealAttendance.value.filter(a => planIds.has(a.mealPlanId) && a.mealPlanId === plan.id)

        const attendees: EventReportAttendee[] = planAttendance
          .filter(a => a.status !== 'NotGoing')
          .map(a => ({
            memberId: a.householdMemberId,
            name: memberById.get(a.householdMemberId)?.name ?? '',
            status: a.status,
            bringOwnFood: a.bringOwnFood,
            // memberById already carries the effective band (derived from birth date, else manual).
            ageBand: memberById.get(a.householdMemberId)?.ageBand ?? null,
            dietary: dietaryForMember(a.householdMemberId),
            dietaryNotes: memberById.get(a.householdMemberId)?.dietaryNotes ?? null,
          }))
          .sort((x, y) =>
            byName(householdNameForMember(x.memberId), householdNameForMember(y.memberId))
            || compareMembersById(x.memberId, y.memberId))

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
          templateId: plan.mealTemplateId,
          templateName: templateNameById.get(plan.mealTemplateId) ?? '',
          mealType: plan.mealType,
          isException: plan.isException ?? false,
          notes: plan.notes ?? null,
          going: planAttendance.filter(a => a.status === 'Going').length,
          maybe: planAttendance.filter(a => a.status === 'Maybe').length,
          notGoing: planAttendance.filter(a => a.status === 'NotGoing').length,
          bringOwnFood: planAttendance.filter(a => a.status !== 'NotGoing' && a.bringOwnFood).length,
          dietary,
          attendees,
        }
      })

      // Tasks for this day, ordered by the shared template scheme (mirrors backend).
      const tasks: EventReportTask[] = taskPlansForEvent
        .filter(p => p.day === day)
        .map((plan): EventReportTask => {
          const template = taskTemplateById.get(plan.templateId)
          const assignees = [...(intentsByPlan.get(plan.id) ?? [])]
            .sort(compareMembersById)
            .map(id => memberById.get(id)?.name ?? '')
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
        .sort((a, b) => compareOrderKeys(
          { slotRank: taskSlotRank(a.timeSlot), ...taskOrderKey.get(`${a.templateId}:${a.timeSlot ?? ''}`)!, title: a.templateName },
          { slotRank: taskSlotRank(b.timeSlot), ...taskOrderKey.get(`${b.templateId}:${b.timeSlot ?? ''}`)!, title: b.templateName },
        ))

      // Every accommodation is emitted on every day (vacant ones report occupied 0) so the badge
      // renders on all days; occupants are the stays whose span covers this night (declined
      // excluded; a party with no adults/children counts as 1).
      const accommodations: EventReportAccommodation[] = eventAccommodations
        .map((accommodation): EventReportAccommodation => {
          const occupants: EventReportOccupant[] = store.accommodationIntents.value
            .filter(i => i.accommodationId === accommodation.id
              && i.startNight <= day && i.endNight >= day
              && i.status !== 'Declined')
            .map((i): EventReportOccupant => ({
              memberId: i.householdMemberId,
              name: memberById.get(i.householdMemberId)?.name ?? '',
              status: i.status ?? 'Requested',
              partyAdults: i.partyAdults ?? null,
              partyChildren: i.partyChildren ?? null,
            }))
            .sort((a, b) =>
              byName(householdNameForMember(a.memberId), householdNameForMember(b.memberId))
              || compareMembersById(a.memberId, b.memberId))

          return {
            accommodationId: accommodation.id,
            name: accommodation.name,
            type: accommodation.type ?? 'Bedroom',
            capacity: sleepsCapacity(accommodation.beds ?? []),
            occupied: occupants.reduce((sum, o) => sum + Math.max((o.partyAdults ?? 0) + (o.partyChildren ?? 0), 1), 0),
            occupants,
          }
        })
        .sort(compareAccommodations)

      days.push({ day, going, maybe, attendees: dayAttendees, meals, tasks, accommodations })
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

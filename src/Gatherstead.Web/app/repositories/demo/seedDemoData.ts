import type { Repositories } from '../interfaces'
import { DEMO_TENANT_ID, DEMO_USER_ID } from './DemoStore'

function nextWeekendStart(): string {
  const today = new Date()
  const day = today.getDay()
  const daysUntilSat = ((6 - day) + 7) % 7 || 7
  const sat = new Date(today)
  sat.setDate(today.getDate() + daysUntilSat)
  return sat.toISOString().substring(0, 10)
}

function addDays(dateStr: string, n: number): string {
  const d = new Date(dateStr)
  d.setDate(d.getDate() + n)
  return d.toISOString().substring(0, 10)
}

export async function seedDemoData(repos: Repositories): Promise<void> {
  // 1. Property
  const property = await repos.properties.createProperty(DEMO_TENANT_ID, 'Camp Nomanisan')

  // 2. Accommodations
  const cabinA = await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'Cabin A', 'Bedroom', 4, 2,
    'Main cabin with lake views. No capes near the fireplace.',
  )
  const cabinB = await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'Cabin B', 'Bedroom', 2, 0, null,
  )
  const rvPad = await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'RV Pad 1', 'RvPad', 2, 2, null,
  )
  await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'Tent Site 1', 'Tent', 4, 0, null,
  )

  // 3. Households
  const parrFamily = await repos.households.createHousehold(DEMO_TENANT_ID, 'The Parr Family')
  const frozoneHousehold = await repos.households.createHousehold(DEMO_TENANT_ID, 'The Frozone Household')
  const ednaStudio = await repos.households.createHousehold(DEMO_TENANT_ID, 'Edna Mode Studio')

  // 4. Members
  const bob = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Bob Parr', true, null, null,
    'Large portions — saving the world burns a lot of calories.', [],
  )
  const helen = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Helen Parr', true, null, null, null, [],
  )
  const violet = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Violet Parr', false, '13-17', null,
    'Will not eat anything if people are watching.', [],
  )
  const dash = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Dash Parr', false, '8-12', null,
    'Eats at top speed. Food must be secured to the plate.', [],
  )
  const jackJack = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Jack-Jack Parr', false, '0-3', null,
    'Baby food only. Keep away from raccoons.', [],
  )

  const lucius = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, frozoneHousehold.id, 'Lucius Best', true, null, null, null, [],
  )
  const honey = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, frozoneHousehold.id, 'Honey Best', true, null, null, null, [],
  )

  const edna = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, ednaStudio.id, 'Edna Mode', true, null, null,
    'No capes. Also no gluten.', ['gluten-free'],
  )

  // Link demo user to Bob Parr
  await repos.tenantUsers.setLinkedMember(DEMO_TENANT_ID, DEMO_USER_ID, bob.id)
  useCurrentMemberStore().setLinkedMember(bob.id, parrFamily.id)

  // 5. Event
  const eventStart = nextWeekendStart()
  const eventEnd = addDays(eventStart, 2)
  const event = await repos.events.createEvent(
    DEMO_TENANT_ID, property.id, 'Super Summer Retreat — Keep It Secret!', eventStart, eventEnd,
  )

  // 6. Meal templates — plans auto-generated per (day × mealType) by createTemplate
  const breakfastTemplate = await repos.mealPlans.createTemplate(
    DEMO_TENANT_ID, event.id, 'Breakfast', 0x01, null, null, null,
  )
  const lunchTemplate = await repos.mealPlans.createTemplate(
    DEMO_TENANT_ID, event.id, 'Lunch', 0x02, null, null, null,
  )
  const dinnerTemplate = await repos.mealPlans.createTemplate(
    DEMO_TENANT_ID, event.id, 'Dinner', 0x04, null, null, null,
  )

  // 7. Task templates — plans auto-generated per (day × timeSlot) by createTemplate
  await repos.tasks.createTemplate(
    DEMO_TENANT_ID, event.id, 'Set Up', 0x01,
    eventStart, eventStart,
    null, 'Prepare the venue before the event.',
  )
  await repos.tasks.createTemplate(
    DEMO_TENANT_ID, event.id, 'Suit Inventory Check', 0x01,
    null, null,
    1, 'Coordinate with Edna. Do NOT ask about capes.',
  )
  await repos.tasks.createTemplate(
    DEMO_TENANT_ID, event.id, 'Keep Dash From Running', 0x08,
    null, null,
    2, 'Two adults minimum. Past attempts with one have failed.',
  )
  await repos.tasks.createTemplate(
    DEMO_TENANT_ID, event.id, 'Tear Down', 0x04,
    eventEnd, eventEnd,
    null, 'Clean up and close out the venue.',
  )

  // 8. Event days
  const day1 = eventStart
  const day2 = addDays(eventStart, 1)
  const day3 = addDays(eventStart, 2)

  // 9. Seed meal attendance
  const breakfastPlans = await repos.mealPlans.listPlans(DEMO_TENANT_ID, event.id, breakfastTemplate.id)
  const lunchPlans = await repos.mealPlans.listPlans(DEMO_TENANT_ID, event.id, lunchTemplate.id)
  const dinnerPlans = await repos.mealPlans.listPlans(DEMO_TENANT_ID, event.id, dinnerTemplate.id)

  const day1Breakfast = breakfastPlans.find(p => p.day === day1)
  const day2Breakfast = breakfastPlans.find(p => p.day === day2)
  const day3Breakfast = breakfastPlans.find(p => p.day === day3)

  const day1Lunch = lunchPlans.find(p => p.day === day1)
  const day2Lunch = lunchPlans.find(p => p.day === day2)
  const day3Lunch = lunchPlans.find(p => p.day === day3)

  const day1Dinner = dinnerPlans.find(p => p.day === day1)
  const day2Dinner = dinnerPlans.find(p => p.day === day2)
  const day3Dinner = dinnerPlans.find(p => p.day === day3)

  if (day1Breakfast) {
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, breakfastTemplate.id, day1Breakfast.id,
      parrFamily.id, bob.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, breakfastTemplate.id, day1Breakfast.id,
      parrFamily.id, helen.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, breakfastTemplate.id, day1Breakfast.id,
      frozoneHousehold.id, lucius.id, 'Going', false,
    )
  }

  if (day1Lunch) {
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id,
      parrFamily.id, bob.id, 'Maybe', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id,
      frozoneHousehold.id, lucius.id, 'Going', false,
    )
  }

  if (day1Dinner) {
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id,
      parrFamily.id, bob.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id,
      parrFamily.id, helen.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id,
      ednaStudio.id, edna.id, 'Going', false,
      'Gluten-free options required.',
    )
  }

  // Violet is not attending — mark NotGoing for all meals on all days
  for (const plan of [day1Breakfast, day2Breakfast, day3Breakfast]) {
    if (plan) {
      await repos.mealAttendance.upsertMealAttendance(
        DEMO_TENANT_ID, event.id, breakfastTemplate.id, plan.id,
        parrFamily.id, violet.id, 'NotGoing', false,
      )
    }
  }
  for (const plan of [day1Lunch, day2Lunch, day3Lunch]) {
    if (plan) {
      await repos.mealAttendance.upsertMealAttendance(
        DEMO_TENANT_ID, event.id, lunchTemplate.id, plan.id,
        parrFamily.id, violet.id, 'NotGoing', false,
      )
    }
  }
  for (const plan of [day1Dinner, day2Dinner, day3Dinner]) {
    if (plan) {
      await repos.mealAttendance.upsertMealAttendance(
        DEMO_TENANT_ID, event.id, dinnerTemplate.id, plan.id,
        parrFamily.id, violet.id, 'NotGoing', false,
      )
    }
  }

  // 10. Event attendance
  // Parr family: all 3 days, Violet not going
  for (const day of [day1, day2, day3]) {
    await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, parrFamily.id, bob.id, day, 'Going')
    await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, parrFamily.id, helen.id, day, 'Going')
    await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, parrFamily.id, violet.id, day, 'NotGoing')
    await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, parrFamily.id, dash.id, day, 'Going')
    await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, parrFamily.id, jackJack.id, day, 'Going')
  }
  // Frozone household: first two days (matches 2-night accommodation)
  for (const day of [day1, day2]) {
    await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, frozoneHousehold.id, lucius.id, day, 'Going')
    await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, frozoneHousehold.id, honey.id, day, 'Going')
  }
  // Edna: first day only (matches 1-night accommodation)
  await repos.eventAttendance.upsertAttendance(DEMO_TENANT_ID, event.id, ednaStudio.id, edna.id, day1, 'Going')

  // 11. Accommodation intents
  // Bob: Cabin A, 3 nights — Confirmed/Approved, party of 5
  for (const night of [day1, day2, day3]) {
    const intent = await repos.accommodationIntents.createIntent(
      DEMO_TENANT_ID, property.id, cabinA.id, parrFamily.id, bob.id, night, 'Confirmed', null, 5,
    )
    await repos.accommodationIntents.updateIntent(
      DEMO_TENANT_ID, property.id, cabinA.id, intent.id, 'Confirmed', 'Approved', null, 5,
    )
  }

  // Lucius: RV Pad, 2 nights — Hold/Pending, party of 2
  await repos.accommodationIntents.createIntent(
    DEMO_TENANT_ID, property.id, rvPad.id, frozoneHousehold.id, lucius.id,
    day1, 'Hold', "Honey said RV or nothing. We'll see.", 2,
  )
  await repos.accommodationIntents.createIntent(
    DEMO_TENANT_ID, property.id, rvPad.id, frozoneHousehold.id, lucius.id,
    day2, 'Hold', null, 2,
  )

  // Edna: Cabin B, 1 night — Confirmed/Approved, party of 1
  const ednaIntent = await repos.accommodationIntents.createIntent(
    DEMO_TENANT_ID, property.id, cabinB.id, ednaStudio.id, edna.id,
    day1, 'Confirmed', 'Separate cabin. Non-negotiable.', 1,
  )
  await repos.accommodationIntents.updateIntent(
    DEMO_TENANT_ID, property.id, cabinB.id, ednaIntent.id,
    'Confirmed', 'Approved', 'Separate cabin. Non-negotiable.', 1,
  )
}

import type { Repositories } from '../interfaces'

const TENANT_ID = 'demo-tenant'

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
  const property = await repos.properties.createProperty(TENANT_ID, 'Camp Nomanisan')

  // 2. Accommodations
  const cabinA = await repos.accommodations.createAccommodation(
    TENANT_ID, property.id, 'Cabin A', 'Bedroom', 4, 2,
    'Main cabin with lake views. No capes near the fireplace.',
  )
  const cabinB = await repos.accommodations.createAccommodation(
    TENANT_ID, property.id, 'Cabin B', 'Bedroom', 2, 0, null,
  )
  const rvPad = await repos.accommodations.createAccommodation(
    TENANT_ID, property.id, 'RV Pad 1', 'RvPad', 2, 2, null,
  )
  await repos.accommodations.createAccommodation(
    TENANT_ID, property.id, 'Tent Site 1', 'Tent', 4, 0, null,
  )

  // 3. Households
  const parrFamily = await repos.households.createHousehold(TENANT_ID, 'The Parr Family')
  const frozoneHousehold = await repos.households.createHousehold(TENANT_ID, 'The Frozone Household')
  const ednaStudio = await repos.households.createHousehold(TENANT_ID, 'Edna Mode Studio')

  // 4. Members
  const bob = await repos.householdMembers.createMember(
    TENANT_ID, parrFamily.id, 'Bob Parr', true, null, null, 'Admin',
    'Large portions — saving the world burns a lot of calories.', [],
  )
  const helen = await repos.householdMembers.createMember(
    TENANT_ID, parrFamily.id, 'Helen Parr', true, null, null, 'Admin', null, [],
  )
  await repos.householdMembers.createMember(
    TENANT_ID, parrFamily.id, 'Violet Parr', false, '13-17', null, 'Member',
    'Will not eat anything if people are watching.', [],
  )
  await repos.householdMembers.createMember(
    TENANT_ID, parrFamily.id, 'Dash Parr', false, '8-12', null, 'Member',
    'Eats at top speed. Food must be secured to the plate.', [],
  )
  await repos.householdMembers.createMember(
    TENANT_ID, parrFamily.id, 'Jack-Jack Parr', false, '0-3', null, 'Member',
    'Baby food only. Keep away from raccoons.', [],
  )

  const lucius = await repos.householdMembers.createMember(
    TENANT_ID, frozoneHousehold.id, 'Lucius Best', true, null, null, 'Admin', null, [],
  )
  await repos.householdMembers.createMember(
    TENANT_ID, frozoneHousehold.id, 'Honey Best', true, null, null, 'Member', null, [],
  )

  const edna = await repos.householdMembers.createMember(
    TENANT_ID, ednaStudio.id, 'Edna Mode', true, null, null, 'Admin',
    'No capes. Also no gluten.', ['Gluten-Free'],
  )

  // 5. Event
  const eventStart = nextWeekendStart()
  const eventEnd = addDays(eventStart, 2)
  const event = await repos.events.createEvent(
    TENANT_ID, property.id, 'Super Summer Retreat — Keep It Secret!', eventStart, eventEnd,
  )

  // 6. Meal templates — plans auto-generated per (day × mealType) by createTemplate
  const breakfastTemplate = await repos.mealPlans.createTemplate(
    TENANT_ID, event.id, 'Breakfast', 0x01, null,
  )
  const lunchTemplate = await repos.mealPlans.createTemplate(
    TENANT_ID, event.id, 'Lunch', 0x02, null,
  )
  const dinnerTemplate = await repos.mealPlans.createTemplate(
    TENANT_ID, event.id, 'Dinner', 0x04, null,
  )

  // 7. Chore templates — plans auto-generated per (day × timeSlot) by createTemplate
  await repos.chores.createTemplate(
    TENANT_ID, event.id, 'Suit Inventory Check', 0x01, 1,
    'Coordinate with Edna. Do NOT ask about capes.',
  )
  await repos.chores.createTemplate(
    TENANT_ID, event.id, 'Keep Dash From Running', 0x08, 2,
    'Two adults minimum. Past attempts with one have failed.',
  )

  // 8. Seed meal attendance for day 1 plans
  const breakfastPlans = await repos.mealPlans.listPlans(TENANT_ID, event.id, breakfastTemplate.id)
  const lunchPlans = await repos.mealPlans.listPlans(TENANT_ID, event.id, lunchTemplate.id)
  const dinnerPlans = await repos.mealPlans.listPlans(TENANT_ID, event.id, dinnerTemplate.id)

  const day1Breakfast = breakfastPlans.find(p => p.day === eventStart)
  const day1Lunch = lunchPlans.find(p => p.day === eventStart)
  const day1Dinner = dinnerPlans.find(p => p.day === eventStart)

  if (day1Breakfast) {
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, breakfastTemplate.id, day1Breakfast.id,
      parrFamily.id, bob.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, breakfastTemplate.id, day1Breakfast.id,
      parrFamily.id, helen.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, breakfastTemplate.id, day1Breakfast.id,
      frozoneHousehold.id, lucius.id, 'Going', false,
    )
  }

  if (day1Lunch) {
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id,
      parrFamily.id, bob.id, 'Maybe', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id,
      frozoneHousehold.id, lucius.id, 'Going', false,
    )
  }

  if (day1Dinner) {
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id,
      parrFamily.id, bob.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id,
      parrFamily.id, helen.id, 'Going', false,
    )
    await repos.mealAttendance.upsertMealAttendance(
      TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id,
      ednaStudio.id, edna.id, 'Going', false,
      'Gluten-free options required.',
    )
  }

  // 9. Accommodation intents
  const night0 = eventStart
  const night1 = addDays(eventStart, 1)
  const night2 = addDays(eventStart, 2)

  // Bob: Cabin A, 3 nights — Confirmed/Approved, party of 5
  for (const night of [night0, night1, night2]) {
    const intent = await repos.accommodationIntents.createIntent(
      TENANT_ID, property.id, cabinA.id, parrFamily.id, bob.id, night, 'Confirmed', null, 5,
    )
    await repos.accommodationIntents.updateIntent(
      TENANT_ID, property.id, cabinA.id, intent.id, 'Confirmed', 'Approved', null, 5,
    )
  }

  // Lucius: RV Pad, 2 nights — Hold/Pending, party of 2
  await repos.accommodationIntents.createIntent(
    TENANT_ID, property.id, rvPad.id, frozoneHousehold.id, lucius.id,
    night0, 'Hold', "Honey said RV or nothing. We'll see.", 2,
  )
  await repos.accommodationIntents.createIntent(
    TENANT_ID, property.id, rvPad.id, frozoneHousehold.id, lucius.id,
    night1, 'Hold', null, 2,
  )

  // Edna: Cabin B, 1 night — Confirmed/Approved, party of 1
  const ednaIntent = await repos.accommodationIntents.createIntent(
    TENANT_ID, property.id, cabinB.id, ednaStudio.id, edna.id,
    night0, 'Confirmed', 'Separate cabin. Non-negotiable.', 1,
  )
  await repos.accommodationIntents.updateIntent(
    TENANT_ID, property.id, cabinB.id, ednaIntent.id,
    'Confirmed', 'Approved', 'Separate cabin. Non-negotiable.', 1,
  )
}

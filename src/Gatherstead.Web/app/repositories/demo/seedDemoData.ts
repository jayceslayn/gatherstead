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

// A birth date `years` ago from today, kept relative so seeded members always land in
// the intended (mid-range) age band regardless of when the demo is loaded.
function birthDateYearsAgo(years: number): string {
  const d = new Date()
  d.setFullYear(d.getFullYear() - years)
  return d.toISOString().substring(0, 10)
}

export async function seedDemoData(repos: Repositories): Promise<void> {
  // 1. Property — with custom attributes scoped to different roles to show off the
  // extensible-attributes feature. tenantMinRole uses the backend TenantRole enum
  // (Owner 0, Manager 1, Coordinator 2, Member 3, Guest 4); an attribute is visible
  // to callers whose role is at least as privileged (callerRole <= tenantMinRole).
  const property = await repos.properties.createProperty(
    DEMO_TENANT_ID, 'Camp Nomanisan', 'Volcanic island retreat. Monorail runs along the north ridge.',
    [
      { key: 'Gate Code', value: '4-7-3-9', tenantMinRole: 2 }, // Coordinator and above
      { key: 'WiFi Network', value: 'NomanisanGuest', tenantMinRole: 3 }, // Members and above
    ],
  )

  // 1b. Equipment — always scoped to a property; one carries a custom attribute.
  await repos.equipment.createEquipment(
    DEMO_TENANT_ID, 'Projector & Screen', property.id,
    'For the Saturday night movie. Keep well away from Jack-Jack.',
    [{ key: 'Storage Location', value: 'Cabin A loft', tenantMinRole: 3 }],
  )
  await repos.equipment.createEquipment(
    DEMO_TENANT_ID, 'Industrial BBQ Grill', property.id, 'Propane. Two spare tanks in the shed.', [],
  )
  await repos.equipment.createEquipment(
    DEMO_TENANT_ID, 'First Aid Kit', property.id, 'Restocked this spring. No capes inside.', [],
  )

  // 2. Accommodations — bed inventory drives the sleeps capacity; dimensions are display-only.
  const cabinA = await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'Cabin A', 'Bedroom',
    { widthMeters: 4, depthMeters: 5, areaSqMeters: null },
    [{ size: 'Queen', quantity: 2 }, { size: 'Bunk', quantity: 1 }],
    'Main cabin with lake views. No capes near the fireplace.',
  )
  const cabinB = await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'Cabin B', 'Bedroom',
    { widthMeters: 3, depthMeters: 3.5, areaSqMeters: null },
    [{ size: 'Double', quantity: 1 }],
    null,
  )
  const rvPad = await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'RV Pad 1', 'RvPad',
    { widthMeters: 4, depthMeters: 10, areaSqMeters: null },
    [],
    null,
  )
  await repos.accommodations.createAccommodation(
    DEMO_TENANT_ID, property.id, 'Tent Site 1', 'Tent',
    { widthMeters: 6, depthMeters: 6, areaSqMeters: null },
    [{ size: 'Single', quantity: 4 }],
    null,
  )

  // 3. Households — the Parr household carries notes + a role-gated custom attribute.
  const parrFamily = await repos.households.createHousehold(
    DEMO_TENANT_ID, 'The Parr Family', 'Keep the secret identities secret.',
    [{ key: 'Emergency Contact', value: 'Lucius Best (Frozone) · 555-0142', tenantMinRole: 2 }], // Coordinator and above
  )
  const frozoneHousehold = await repos.households.createHousehold(DEMO_TENANT_ID, 'The Frozone Household')
  const ednaStudio = await repos.households.createHousehold(DEMO_TENANT_ID, 'Edna Mode Studio')

  // 4. Members — a deliberate mix of birth-date-derived bands and manual-only bands.
  // Birth date present → age band is derived (and read-only in the form); birth date
  // absent → the manual band is used as the fallback.
  const bob = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Bob Parr', null, birthDateYearsAgo(45),
    'Large portions — saving the world burns a lot of calories.',
    'Prefers a downstairs room — the knees are not what they used to be.', [],
    [{ key: 'Supersuit Size', value: 'XL (reinforced seams)', tenantMinRole: 3 }], // Members and above
  )
  const helen = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Helen Parr', 'Age18To64', null, null, null, [],
  )
  const violet = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Violet Parr', null, birthDateYearsAgo(15),
    'Will not eat anything if people are watching.', null, [],
  )
  const dash = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Dash Parr', 'Age6To12', null,
    'Eats at top speed. Food must be secured to the plate.', null, [],
  )
  const jackJack = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, parrFamily.id, 'Jack-Jack Parr', null, birthDateYearsAgo(1),
    'Baby food only. Keep away from raccoons.', null, [],
  )

  const lucius = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, frozoneHousehold.id, 'Lucius Best', 'Age18To64', null, null, null, [],
  )
  const honey = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, frozoneHousehold.id, 'Honey Best', null, birthDateYearsAgo(42), null, null, [],
  )

  const edna = await repos.householdMembers.createMember(
    DEMO_TENANT_ID, ednaStudio.id, 'Edna Mode', 'Age65Plus', null,
    'No capes. Also no gluten.',
    'Do not, under any circumstances, discuss capes.', ['gluten-free'],
  )

  // Link demo user to Bob Parr
  await repos.tenantUsers.setLinkedMember(DEMO_TENANT_ID, DEMO_USER_ID, bob.id)
  useCurrentMemberStore().setLinkedMember(bob.id, parrFamily.id)

  // 5. Event
  const eventStart = nextWeekendStart()
  const eventEnd = addDays(eventStart, 2)
  const event = await repos.events.createEvent(
    DEMO_TENANT_ID, property.id, 'Super Summer Retreat — Keep It Secret!', eventStart, eventEnd,
    'Annual gathering of the Supers. Civilian clothes on arrival.',
    [
      { key: 'Dress Code', value: 'Secret identities encouraged', tenantMinRole: 3 }, // Members and above
      { key: 'Emergency Contact', value: 'Mirage · 555-0100', tenantMinRole: 1 }, // Manager and above
    ],
  )

  // 6. Meal templates — plans auto-generated per (day × mealType) by createTemplate
  const breakfastTemplate = await repos.mealPlans.createTemplate(
    DEMO_TENANT_ID, event.id, 'Breakfast', 0x01, null, null, null,
  )
  const lunchTemplate = await repos.mealPlans.createTemplate(
    DEMO_TENANT_ID, event.id, 'Lunch', 0x02, null, null, null,
  )
  // Dinner also spins up a matching task template ("Dinner", Evening slot) via the
  // createMatchingTaskTemplate flag — the same flow the meal-template modal exposes.
  const dinnerTemplate = await repos.mealPlans.createTemplate(
    DEMO_TENANT_ID, event.id, 'Dinner', 0x04, null, null, null, null, true,
  )

  // 7. Task templates — plans auto-generated per (day × timeSlot) by createTemplate
  const setUpTemplate = await repos.tasks.createTemplate(
    DEMO_TENANT_ID, event.id, 'Set Up', 0x01,
    eventStart, eventStart,
    null, 'Prepare the venue before the event.',
  )
  const suitCheckTemplate = await repos.tasks.createTemplate(
    DEMO_TENANT_ID, event.id, 'Suit Inventory Check', 0x01,
    null, null,
    1, 'Coordinate with Edna. Do NOT ask about capes.',
  )
  const keepDashTemplate = await repos.tasks.createTemplate(
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

  // 8b. Seed task intents — a deliberate coverage mix so the report shows Covered/Partial/Open.
  // "Set Up" (no minimum): Bob + Helen volunteer on day 1 → covered.
  const setUpPlans = await repos.tasks.listPlans(DEMO_TENANT_ID, event.id, setUpTemplate.id)
  const day1SetUp = setUpPlans.find(p => p.day === day1)
  if (day1SetUp) {
    await repos.tasks.upsertIntent(DEMO_TENANT_ID, event.id, setUpTemplate.id, day1SetUp.id, parrFamily.id, bob.id)
    await repos.tasks.upsertIntent(DEMO_TENANT_ID, event.id, setUpTemplate.id, day1SetUp.id, parrFamily.id, helen.id)
  }
  // "Suit Inventory Check" (min 1): Edna covers day 1; days 2–3 left open.
  const suitCheckPlans = await repos.tasks.listPlans(DEMO_TENANT_ID, event.id, suitCheckTemplate.id)
  const day1SuitCheck = suitCheckPlans.find(p => p.day === day1)
  if (day1SuitCheck) {
    await repos.tasks.upsertIntent(DEMO_TENANT_ID, event.id, suitCheckTemplate.id, day1SuitCheck.id, ednaStudio.id, edna.id)
  }
  // "Keep Dash From Running" (min 2): only Helen volunteers on day 1 → partial; rest open.
  const keepDashPlans = await repos.tasks.listPlans(DEMO_TENANT_ID, event.id, keepDashTemplate.id)
  const day1KeepDash = keepDashPlans.find(p => p.day === day1)
  if (day1KeepDash) {
    await repos.tasks.upsertIntent(DEMO_TENANT_ID, event.id, keepDashTemplate.id, day1KeepDash.id, parrFamily.id, helen.id)
  }
  // "Dinner" (auto-generated from the Dinner meal template, no minimum): Bob cooks day 1,
  // Helen day 2 → covered; day 3 left open.
  const dinnerTaskTemplate = (await repos.tasks.listTaskTemplates(DEMO_TENANT_ID, event.id))
    .find(t => t.name === 'Dinner')
  if (dinnerTaskTemplate) {
    const dinnerTaskPlans = await repos.tasks.listPlans(DEMO_TENANT_ID, event.id, dinnerTaskTemplate.id)
    const day1DinnerTask = dinnerTaskPlans.find(p => p.day === day1)
    const day2DinnerTask = dinnerTaskPlans.find(p => p.day === day2)
    if (day1DinnerTask) {
      await repos.tasks.upsertIntent(DEMO_TENANT_ID, event.id, dinnerTaskTemplate.id, day1DinnerTask.id, parrFamily.id, bob.id)
    }
    if (day2DinnerTask) {
      await repos.tasks.upsertIntent(DEMO_TENANT_ID, event.id, dinnerTaskTemplate.id, day2DinnerTask.id, parrFamily.id, helen.id)
    }
  }

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

  // Day 1 breakfast exclusion — guests arrive mid-day; meal not provided on site.
  if (day1Breakfast) {
    await repos.mealPlans.deletePlan(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day1Breakfast.id)
  }

  // Day 1 lunch — first meal on site; a mix of Going / Maybe as people trickle in.
  if (day1Lunch) {
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, parrFamily.id, bob.id, 'Maybe', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, parrFamily.id, helen.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, parrFamily.id, violet.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, parrFamily.id, dash.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, parrFamily.id, jackJack.id, 'Maybe', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, frozoneHousehold.id, lucius.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, frozoneHousehold.id, honey.id, 'Maybe', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day1Lunch.id, ednaStudio.id, edna.id, 'Maybe', false)
  }

  // Day 1 dinner — most people settled in by evening.
  if (day1Dinner) {
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, parrFamily.id, bob.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, parrFamily.id, helen.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, parrFamily.id, violet.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, parrFamily.id, dash.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, parrFamily.id, jackJack.id, 'Maybe', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, frozoneHousehold.id, lucius.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, frozoneHousehold.id, honey.id, 'Maybe', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day1Dinner.id, ednaStudio.id, edna.id, 'Going', false, 'Gluten-free options required.')
  }

  // Day 2 breakfast — full turnout for the Parrs; Lucius skips all day-2 meals.
  if (day2Breakfast) {
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day2Breakfast.id, parrFamily.id, bob.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day2Breakfast.id, parrFamily.id, helen.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day2Breakfast.id, parrFamily.id, violet.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day2Breakfast.id, parrFamily.id, dash.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day2Breakfast.id, parrFamily.id, jackJack.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day2Breakfast.id, frozoneHousehold.id, lucius.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day2Breakfast.id, frozoneHousehold.id, honey.id, 'Going', false)
  }

  // Day 2 lunch
  if (day2Lunch) {
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day2Lunch.id, parrFamily.id, bob.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day2Lunch.id, parrFamily.id, helen.id, 'Maybe', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day2Lunch.id, parrFamily.id, violet.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day2Lunch.id, parrFamily.id, dash.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day2Lunch.id, parrFamily.id, jackJack.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day2Lunch.id, frozoneHousehold.id, lucius.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day2Lunch.id, frozoneHousehold.id, honey.id, 'Going', false)
  }

  // Day 2 dinner
  if (day2Dinner) {
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day2Dinner.id, parrFamily.id, bob.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day2Dinner.id, parrFamily.id, helen.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day2Dinner.id, parrFamily.id, violet.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day2Dinner.id, parrFamily.id, dash.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day2Dinner.id, parrFamily.id, jackJack.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day2Dinner.id, frozoneHousehold.id, lucius.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day2Dinner.id, frozoneHousehold.id, honey.id, 'Going', false)
  }

  // Day 3 breakfast — last morning before packing up; Parrs still Going.
  if (day3Breakfast) {
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day3Breakfast.id, parrFamily.id, bob.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day3Breakfast.id, parrFamily.id, helen.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day3Breakfast.id, parrFamily.id, violet.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day3Breakfast.id, parrFamily.id, dash.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, breakfastTemplate.id, day3Breakfast.id, parrFamily.id, jackJack.id, 'Going', false)
  }

  // Day 3 lunch — heading out soon; kids are a Maybe.
  if (day3Lunch) {
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day3Lunch.id, parrFamily.id, bob.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day3Lunch.id, parrFamily.id, helen.id, 'Going', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day3Lunch.id, parrFamily.id, violet.id, 'NotGoing', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day3Lunch.id, parrFamily.id, dash.id, 'Maybe', false)
    await repos.mealAttendance.upsertMealAttendance(DEMO_TENANT_ID, event.id, lunchTemplate.id, day3Lunch.id, parrFamily.id, jackJack.id, 'Maybe', false)
  }

  // Day 3 dinner exclusion — guests depart before evening; meal not provided on site.
  if (day3Dinner) {
    await repos.mealPlans.deletePlan(DEMO_TENANT_ID, event.id, dinnerTemplate.id, day3Dinner.id)
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

  // 11. Accommodation stays (each is a single span of nights). The merged status enum lets seeds
  // create rows in their final state directly (no separate decision step).
  // Bob: Cabin A, day1–day3 — Confirmed, party of 3 adults + 2 children
  await repos.accommodationIntents.createIntent(
    DEMO_TENANT_ID, property.id, cabinA.id, parrFamily.id, bob.id, day1, day3, 'Confirmed', null, 3, 2,
  )

  // Lucius: RV Pad, day1–day2 — Hold, party of 2 adults
  await repos.accommodationIntents.createIntent(
    DEMO_TENANT_ID, property.id, rvPad.id, frozoneHousehold.id, lucius.id,
    day1, day2, 'Hold', "Honey said RV or nothing. We'll see.", 2, null,
  )

  // Edna: Cabin B, day1 only — Confirmed, party of 1 adult
  await repos.accommodationIntents.createIntent(
    DEMO_TENANT_ID, property.id, cabinB.id, ednaStudio.id, edna.id,
    day1, day1, 'Confirmed', 'Separate cabin. Non-negotiable.', 1, null,
  )

  // 12. Shopping items — one property staple, one event supply, and a couple of meal
  // ingredients on the dinner plans, in a mix of fulfillment states to show the flow.
  await repos.shoppingItems.create(DEMO_TENANT_ID, {
    propertyId: property.id, name: 'Aluminum foil', quantityNeeded: 2, unit: 'rolls',
    category: 'Supplies', notes: 'Running low in Cabin A kitchen.',
  })
  await repos.shoppingItems.create(DEMO_TENANT_ID, {
    eventId: event.id, name: 'Party balloons', quantityNeeded: 3, unit: 'bags',
    category: 'Supplies', notes: 'Red and black — for the Saturday banquet.',
  })

  if (day1Dinner) {
    const potatoes = await repos.shoppingItems.create(DEMO_TENANT_ID, {
      mealPlanId: day1Dinner.id, name: 'Potatoes', quantityNeeded: 10, unit: 'lbs', category: 'Food',
    })
    // Partially supplied: Bob has brought 5 of 10 lbs — someone still needs the rest.
    await repos.shoppingItems.upsertIntent(DEMO_TENANT_ID, potatoes.id, bob.id, { status: 'Provided', quantity: 5 })
    await repos.shoppingItems.create(DEMO_TENANT_ID, {
      mealPlanId: day1Dinner.id, name: 'Butter', quantityNeeded: 2, unit: 'sticks', category: 'Food',
    })
  }
  if (day2Dinner) {
    const steaks = await repos.shoppingItems.create(DEMO_TENANT_ID, {
      mealPlanId: day2Dinner.id, name: 'Ribeye steaks', quantityNeeded: 12, unit: 'count', category: 'Food',
    })
    await repos.shoppingItems.upsertIntent(DEMO_TENANT_ID, steaks.id, helen.id, { status: 'Provided', quantity: 12 })
  }
}

# Plan: Shared Form Components for Create / Edit Pages

## Context

The WEB-UI-DESIGN.md route map lists separate `create.vue` and `edit.vue` files for every entity.
The user asked whether create and edit pages for the same entity can share code, and confirmed that
routes for Events and Accommodations should reflect their parent Property relationship (both are
child entities of Property, not independent top-level entities for creation purposes).

---

## Analysis: Differences Between Create and Edit

| Concern | Create | Edit |
|---|---|---|
| Form fields | Same | Same |
| Field validation | Same | Same |
| Initial state | Empty defaults | Fetched from API |
| Loading state | None needed | `pending` while fetching |
| Page/breadcrumb title | "New [Entity]" | "Edit [Entity Name]" |
| Breadcrumbs | parent → (no detail) | parent → detail → edit |
| API method & endpoint | POST to collection | PUT to `/…/:id` |
| Post-submit navigation | → new entity detail (using returned ID) | → same entity detail |
| Cancel destination | → parent list | → entity detail |

The form fields and their validation are identical. Everything else is a thin wrapper concern.

---

## Recommendation: Shared Form Components + Separate Thin Page Wrappers

**The form markup lives once**, in a component like `GsEventForm.vue`. The create and edit pages
remain separate files but become thin — ~30-40 lines each — handling the wrapper concerns above.

This is preferable to a single `create-or-edit.vue` page with mode detection because each page file
is independently readable, and route intent stays explicit in the file system.

---

## Form Component Contract

Each form component follows this pattern — fields only, no submit button, no fetch, no breadcrumb:

```vue
<!-- GsEventForm.vue -->
<script setup lang="ts">
const props = defineProps<{ modelValue: EventFormState }>()
const emit = defineEmits<{ 'update:modelValue': [EventFormState] }>()
</script>
<template>
  <!-- UForm fields only -->
</template>
```

The page wrapper owns: `UForm @submit`, submit + cancel buttons, breadcrumbs, page title,
`pending` loading state (edit only), `isDirty` dirty tracking, and `navigateTo` after submit.

---

## Route Architecture Change

Events and Accommodations are child entities of Property. Their create/edit routes move under
`/app/properties/[propertyId]/…` to reflect that. Event *detail* and the cross-property event
list remain at `/app/events/…` since they are navigation destinations reachable from the calendar.

The "Create Event" entry point moves from the Events index to the Property detail page (Manager+).

### Updated route map (changes to WEB-UI-DESIGN.md §2)

**Events** — browsing stays top-level; create/edit move under the parent property:
```
/app/events/
  index.vue                         Event list + calendar toggle (unchanged — all events)
  [eventId]/
    index.vue                       Event detail hub / tabs (unchanged — deep-link target)
    attendance.vue                  Attendance grid (unchanged)
    meals/…                         (unchanged)
    chores/…                        (unchanged)
    ~~create.vue~~                  REMOVED — moves under properties
    ~~edit.vue~~                    REMOVED — moves under properties

/app/properties/[propertyId]/
  index.vue                         Property detail (gets "Create Event" button, Manager+)
  events/
    create.vue                      NEW — Create event for this property (Manager+)
    [eventId]/
      edit.vue                      NEW — Edit event (Manager+)
  accommodations/
    create.vue                      NEW — Add accommodation (replaces modal approach)
    [accommodationId]/
      intents.vue                   (unchanged)
      edit.vue                      NEW — Edit accommodation (Manager+)
```

**Directory** — unchanged structure; create routes added:
```
/app/directory/
  index.vue                         Household list (unchanged)
  create.vue                        NEW — Create household (Manager+)
  [householdId]/
    index.vue                       Household detail (unchanged)
    [memberId]/
      index.vue                     Member detail (unchanged)
      edit.vue                      (unchanged — refactored to use GsMemberForm)
      create.vue                    NEW — Add member to this household (Manager+)
```

---

## All Create / Edit Pages

### Events (under parent property)

| Page | Route | Form component | Composable action |
|---|---|---|---|
| Create event | `properties/[propertyId]/events/create.vue` | `GsEventForm.vue` | `useEventActions().createEvent()` |
| Edit event | `properties/[propertyId]/events/[eventId]/edit.vue` | `GsEventForm.vue` | `useEventActions().updateEvent()` |

### Properties

| Page | Route | Form component | Composable action |
|---|---|---|---|
| Create property | `properties/create.vue` | `GsPropertyForm.vue` | `usePropertyActions().createProperty()` |
| Edit property | `properties/[propertyId]/edit.vue` | `GsPropertyForm.vue` | `usePropertyActions().updateProperty()` |

### Accommodations (under parent property)

| Page | Route | Form component | Composable action |
|---|---|---|---|
| Create accommodation | `properties/[propertyId]/accommodations/create.vue` | `GsAccommodationForm.vue` | `useAccommodationActions().createAccommodation()` |
| Edit accommodation | `properties/[propertyId]/accommodations/[accommodationId]/edit.vue` | `GsAccommodationForm.vue` | `useAccommodationActions().updateAccommodation()` |

### Directory

| Page | Route | Form component | Composable action |
|---|---|---|---|
| Create household | `directory/create.vue` | `GsHouseholdForm.vue` | `useHouseholdActions().createHousehold()` |
| Add member | `directory/[householdId]/[memberId]/create.vue` | `GsMemberForm.vue` | `useHouseholdMemberActions().createMember()` |
| Edit member | `directory/[householdId]/[memberId]/edit.vue` | `GsMemberForm.vue` | `useHouseholdMemberActions().updateMember()` |

---

## Files to Create / Modify

**New shared form components** (`app/components/`):
- `GsEventForm.vue` — name, start date, end date (property is implicit from parent route param)
- `GsPropertyForm.vue` — name
- `GsAccommodationForm.vue` — name, type (USelect), adults capacity, children capacity
- `GsHouseholdForm.vue` — name
- `GsMemberForm.vue` — extracted from `edit.vue`: name, isAdult, ageBand, birthDate, dietaryNotes, dietaryTagsInput

**New pages** (8 total):
- `app/pages/app/properties/create.vue`
- `app/pages/app/properties/[propertyId]/edit.vue`
- `app/pages/app/properties/[propertyId]/events/create.vue`
- `app/pages/app/properties/[propertyId]/events/[eventId]/edit.vue`
- `app/pages/app/properties/[propertyId]/accommodations/create.vue`
- `app/pages/app/properties/[propertyId]/accommodations/[accommodationId]/edit.vue`
- `app/pages/app/directory/create.vue`
- `app/pages/app/directory/[householdId]/[memberId]/create.vue`

**Modified pages** (1):
- `app/pages/app/directory/[householdId]/[memberId]/edit.vue` — extract form fields into `GsMemberForm.vue`

**Modified docs** (1):
- `docs/agents/plans/WEB-UI-DESIGN.md` §2 route map — update to reflect property-nested event/accommodation create+edit routes

**Composables** — no changes needed; all create/update actions already exist:
- `useEventActions()` in `app/composables/useEvents.ts`
- `usePropertyActions()` in `app/composables/useProperties.ts`
- `useAccommodationActions()` in `app/composables/useAccommodations.ts`
- `useHouseholdActions()` in `app/composables/useHouseholds.ts`
- `useHouseholdMemberActions()` in `app/composables/useHouseholdMembers.ts`

---

## Verification

- `pnpm build` from `src/Gatherstead.Web/` — 0 errors
- `pnpm run lint` — 0 warnings
- Create form for each entity: fields render empty, breadcrumbs point to parent, submit POSTs and redirects to new entity detail
- Edit form for each entity: fields populate from fetch, breadcrumbs include detail link, submit PUTs and redirects back to detail
- Navigate away with unsaved changes → `unsavedChanges` prompt appears on all forms
- Events index calendar still renders; event detail tabs still accessible via `/app/events/[eventId]`
- "Create Event" button visible on property detail page (Manager+), absent on events index

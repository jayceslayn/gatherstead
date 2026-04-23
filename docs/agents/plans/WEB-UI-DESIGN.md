# Gatherstead Web UI Design Plan

## Context

The app needs a full frontend UI built on the existing Nuxt 4 + Vue 3 + Nuxt UI v4 + Tailwind foundation. The shell is in place (landing page, auth flow, tenant routing, sidebar layout), but all tenant-scoped pages are placeholders. This plan designs the full UI architecture — navigation, routes, pages, components, and state — before any implementation begins.

Design north stars:
- **Clean and simple for regular members**, progressively richer for Managers and Owners
- **Mobile-first** — a high fraction of users will access via phone; every layout, component, and interaction must work well on small screens before desktop
- The calendar is the primary information surface
- Tenant-switching is tucked away like Azure Portal's directory switcher

---

## Tech Foundation (already in place)

- `src/Gatherstead.Web/app/layouts/default.vue` — sidebar + main (authenticated)
- `src/Gatherstead.Web/app/layouts/landing.vue` — header + footer (public)
- `src/Gatherstead.Web/app/composables/useAuth.ts` — auth, logout
- `src/Gatherstead.Web/app/composables/useTenants.ts` — tenant fetch
- `src/Gatherstead.Web/i18n/locales/en.json` — all strings externalized (nav, household, member, event, property keys already exist)
- `src/Gatherstead.Web/app.config.ts` — color tokens: `primary=forest`, `secondary=harvest`, `success=sage`, `neutral=warmstone`
- **FullCalendar**: `@fullcalendar/core` + `@fullcalendar/vue3` installed; `@fullcalendar/daygrid` and `@fullcalendar/list` will need to be added
- **Pinia**: installed, not yet used — needs stores

---

## 1. Navigation Architecture

### Responsive Navigation Strategy

On **mobile** (`< md` breakpoint): the sidebar is hidden and replaced by a **bottom tab bar** fixed to the viewport bottom — 4 primary nav tabs (Dashboard, Directory, Events, Properties) each with icon + label. A "More" tab opens a `UDrawer` for secondary items (Reports, Settings, account menu). This must be implemented in Phase 1 alongside the desktop sidebar — mobile nav is a core requirement, not a future enhancement.

On **desktop** (`md+`): the sidebar is shown as described below.

### Sidebar Refactor (`layouts/default.vue`)

Replace the single-item nav with a full role-aware structure:

```
[Logo → /app/]
[Tenant name — small muted text below logo]

PRIMARY NAV (all authenticated users)
  Dashboard         i-heroicons-home
  Directory         i-heroicons-user-group
  Events            i-heroicons-calendar-days
  Properties        i-heroicons-building-office-2

MANAGEMENT NAV (Manager+ only — v-if gated)
  ── separator ──
  Reports           i-heroicons-chart-bar

BOTTOM (always visible)
  [LocaleSwitcher — stays as-is]
  [User avatar + display name] → UDropdownMenu
```

### User Account Dropdown (replaces the current sign-out button)

A `UDropdownMenu` on the user avatar/name at the sidebar bottom:
1. **Your profile** — links to linked member detail (or disabled if not linked)
2. **Switch group** — navigates to `/tenants` (this is the only prominent tenant-switch path)
3. **Settings** — links to `/app/settings` (hidden for Member role)
4. **Sign out**

This satisfies the requirement that tenant switching not be prominent in the main nav.

---

## 2. Full Route Map

Active tenant is managed entirely by Pinia (`useTenantStore`) and persisted via the existing `last_tenant_id` cookie. `[tenantId]` is **removed from all sub-routes** — the tenant is an implicit context, not a URL parameter.

The `/tenants` selection page remains for users with multiple tenants (or when no `last_tenant_id` cookie exists). After selection, `useTenantStore.setTenant()` is called and the user is redirected to `/app`.

SSR should remain disabled for all authenticated routes via `routeRules: { '/app/**': { ssr: false } }` in `nuxt.config.ts` (or keep the existing `/tenants/**` rule and redirect). The exact URL prefix is a minor implementation choice — `/app/` prefix is clean and avoids collisions with the public `/` landing.

```
/ — landing (public, prerendered) — stays as-is
/tenants — tenant selection — stays as-is (redirects to /app after selection)

/app/                                    Dashboard (replaces /tenants/[tenantId])

/app/directory/
  index.vue                              Household list
  [householdId]/
    index.vue                            Household detail (member table)
    [memberId]/
      index.vue                          Member detail
      edit.vue                           Member edit form
  dietary-summary.vue                    Dietary aggregation (Manager+)

/app/events/
  index.vue                              Event list + calendar toggle
  create.vue                             Create event (Manager+)
  [eventId]/
    index.vue                            Event detail hub (tabbed)
    attendance.vue                       Full attendance grid (Manager+)
    meals/
      index.vue                          Meal plan view
      [templateId]/index.vue             Template detail + daily intents
    chores/
      index.vue                          Chore plan view
      [templateId]/index.vue             Template detail + signup grid
    edit.vue                             Edit event (Manager+)

/app/properties/
  index.vue                              Property list
  create.vue                             Create property (Manager+)
  [propertyId]/
    index.vue                            Property detail + accommodation grid
    edit.vue                             Edit property (Manager+)
    accommodations/
      [accommodationId]/
        intents.vue                      Intent management (per-night)

/app/settings/
  index.vue                              Settings hub (Manager+ guard)
  roles.vue                              Tenant user role table (Owner+)
  danger.vue                             Rename / delete tenant (Owner+)
```

**Tenant context on hard refresh**: A `tenant.ts` middleware (runs on all `/app/**` routes) checks `useTenantStore` — if empty, reads `last_tenant_id` cookie and fetches the tenant name from the API to repopulate the store. If the cookie is missing or invalid, redirect to `/tenants`.

**Deep-linking tradeoff**: URLs like `/app/events/abc` do not encode which tenant the event belongs to. This is acceptable because most users belong to exactly one tenant, and the small number of multi-tenant users switch groups via the account menu. If multi-tenant deep-linking becomes a requirement later, `[tenantId]` can be reintroduced without breaking the rest of the design.

**~22 new pages** under `/app/`.

---

## 3. Page Designs

### Dashboard (`app/index.vue`)

**Mobile-first**: single-column stack on mobile, three-column grid on desktop (`md:grid-cols-3`). Answers: *what's coming up, what do I need to do, who's in my family?*

**Left** — Upcoming Events strip: next 2–3 events as `UCard`, each showing event name, property, date range, and a Going/Maybe/NotGoing quick-action toggle for the current user.

**Center** — Personal calendar: FullCalendar `listWeek` scoped to events where the user's linked member has attendance data. Small, orientation-focused, not full planning.

**Right** — My tasks: unanswered meal intents, open chore slots, accommodation status.

**Manager+ additions** (below the 3-column strip): tenant-wide headcount for next event, dietary restriction alert with link to aggregation view, quick "Manage event" link.

**Empty/first-run**: friendly centered card. Manager+ sees "Create your first event" CTA; Members see "Waiting for an event — check back soon."

---

### Event List (`events/index.vue`)

**View toggle** (top-right, persisted in `localStorage`): Month | Week | List

- **Calendar view**: FullCalendar `dayGridMonth` or `dayGridWeek` showing events as colored blocks (`harvest`). Manager+ sees "+" on day cells.
- **List view**: `UTable` with Name, Property, Dates, My Attendance. Sortable, searchable.
- "Create Event" button (Manager+ only) always visible in page header.

---

### Event Detail (`events/[eventId]/index.vue`)

Four-tab layout via `UTabs`:

**Overview (default)** — Event header (name, property, date range) + FullCalendar `dayGridMonth` scoped to event dates, showing per-day headcount as event numbers. Below: per-day attendance quick-action row for the current user (chip per day with Going/Maybe/NotGoing icons).

**Meals** — Meal plan view. List of MealTemplates (collapsible), each showing a day × meal-type grid. Members see "My response" toggle per cell. Manager+ sees all member responses and exception controls.

**Chores** — Chore plan view. List of ChoreTemplates (collapsible), each showing a day × time-slot grid. Members see "Sign up" button. Manager+ sees volunteer list per slot.

**Accommodations** — Read-only cross-reference: who is staying where during this event. Links to full intent management under Properties.

---

### Attendance Manager (`events/[eventId]/attendance.vue`) — Manager+ only

`UTable`: rows = members, columns = each day in event range. Each cell: status icon (green check / orange ? / muted X / empty). Clicking a cell opens `UModal` to edit that member's attendance + arrival/departure windows. Filter: "Only Not Responded". Summary row with per-day counts.

---

### Family Directory

**Household List** (`directory/index.vue`): search bar + card grid. Each card: household name, member count badge, avatar stack (initials), "View" button. Manager+ sees "+ Create Household."

**Household Detail** (`directory/[householdId]/index.vue`): member `UTable` (Name, Age band, Primary contact, Dietary flag icon). Clicking a row → member detail.

**Member Detail** (`directory/[householdId]/[memberId]/index.vue`): Two-column layout. Left: identity (name, birthdate, addresses, contacts, relationships as chips). Right: dietary profile (preferred diet, allergies as `harvest` badges, restrictions as `sage` badges, notes). Edit button visible to self, HouseholdAdmin, Manager+.

**Member Edit** (`directory/[householdId]/[memberId]/edit.vue`): Single-column `UForm` + Zod schema. Fields: name, birthdate, is-adult, dietary profile, addresses, contacts, custom attributes. Unsaved changes prompt `UModal` confirmation on navigate-away.

**Dietary Aggregation** (`directory/dietary-summary.vue`) — Manager+ only: scope filter (All members / Event attendees). Results as `UTable` + summary badges ("3 vegetarians", "2 nut allergies"). Export button placeholder.

---

### Properties

**Property List** (`properties/index.vue`): card grid. Card: name, accommodation count, "View." Manager+ sees "+ Add Property."

**Property Detail** (`properties/[propertyId]/index.vue`): accommodation grid grouped by type (Bedrooms, Bunks, RV Pads, Tent Sites, Offsite). Each accommodation as a `GsAccommodationCard` with capacity and current intent status badge. "Add Accommodation" (Manager+) per group.

**Accommodation Intents** (`properties/[propertyId]/accommodations/[accommodationId]/intents.vue`): date range filter (defaults to next upcoming event). Per-night intent list with status chips. Manager+ sees promotion controls (Intent → Hold → Confirmed, Decline). Member sees only their own intent + "Request/Update" button.

---

### Settings (`settings/index.vue` + sub-pages) — Manager+ guard

Inner left-nav within the content area (not in sidebar). Sections:

- **General** (Manager+): tenant name display; member role assignments
- **Roles & Access** (Owner+): `UTable` of TenantUsers with inline role `USelect`; remove user with confirm modal
- **Danger Zone** (Owner+): rename tenant, delete tenant (requires typing tenant name to confirm)

---

## 4. Visual Status Language

A consistent icon + color system applied across the calendar, day-intent grids, and cards so users can parse coverage and status at a glance — without reading labels. Color is never used as the *only* differentiator (accessibility).

### Coverage States (MealPlan, ChorePlan)

Applied to `GsDayIntentGrid` cells and calendar event blocks:

| State | Color token | Icon | Meaning |
|---|---|---|---|
| **Covered** | `success` (sage green) | `i-heroicons-check-circle` | Enough Going intents to meet the plan (or ≥ MinimumAssignees for chores) |
| **Partial** | `secondary` (harvest orange) | `i-heroicons-clock` | Some responses but below the threshold, or mix of Going + Maybe |
| **Open** | `neutral` (warmstone, muted) | `i-heroicons-question-mark-circle` | No responses yet — needs attention |
| **Exception** | `primary` (forest, outlined) | `i-heroicons-x-circle` | Manager has flagged this plan as an exception (meal cancelled, chore reassigned, etc.) |

For chores specifically, a small volunteer count badge (e.g. "2/3") overlays the cell so Managers can see coverage numerically.

### Attendance States (EventAttendance, MealIntent, ChoreIntent)

Applied to toggle buttons, table cells, and dashboard task chips:

| Status | Color | Icon |
|---|---|---|
| **Going** | `success` | `i-heroicons-check` |
| **Maybe** | `secondary` (harvest) | `i-heroicons-question-mark` |
| **Not Going** | `neutral` (muted, not error — absence is not a failure) | `i-heroicons-x-mark` |
| **No response** | `neutral` (faded) | `i-heroicons-minus` |

BYOF (bring your own food) on MealIntent: a small fork icon overlays the Going/Maybe badge.

### Accommodation Intent States

| Status | Color token | Icon |
|---|---|---|
| **Intent** (requested) | `neutral` (warmstone) | `i-heroicons-hand-raised` |
| **Hold** | `secondary` (harvest) | `i-heroicons-pause-circle` |
| **Confirmed** | `success` (sage) | `i-heroicons-lock-closed` |
| **Declined** | `error` | `i-heroicons-x-circle` |

### Calendar Event Blocks (`GsCalendar`)

Events on the main calendar use `harvest` as the base event color. A small icon or dot overlay on each event block communicates the current user's attendance status:
- Green dot = Going
- Orange dot = Maybe
- No dot = No response yet

For Manager+ views, the event block can show a headcount chip (e.g. "12 Going").

### Implementation Note

The `GsStatusBadge` component centralizes all status-to-color-to-icon mapping. Every place in the UI that renders a status passes through this component rather than defining colors/icons inline. This ensures the visual language stays consistent as new views are added.

---

## 5. Reusable Components (`app/components/`)

| Component | Purpose | Built on |
|---|---|---|
| `GsCalendar` | FullCalendar wrapper: locale binding, OKLCH color tokens, nav via `UButton`, event-click handler | `@fullcalendar/vue3` |
| `GsBreadcrumb` | Tenant-aware breadcrumb; always starts at tenant name | `UBreadcrumb` |
| `GsEmptyState` | Icon + heading + body + optional action slot; role-aware CTA visibility | `UIcon`, `UButton` |
| `GsConfirmModal` | Reusable confirm/delete dialog with customizable body and button color | `UModal` |
| `GsMemberAvatar` | Initials circle, stackable for groups | Tailwind |
| `GsRoleGate` | Renderless wrapper: `<GsRoleGate min-role="Manager">` renders slot only if role met | `useTenantRole` |
| `GsStatusBadge` | Centralized status-to-color-to-icon mapping for all domain statuses | `UBadge` |
| `GsAttendanceToggle` | Three-button Going/Maybe/NotGoing toggle group | `UButton` |
| `GsDietaryTags` | Allergy chips (`harvest`) + restriction chips (`sage`) | `UBadge` |
| `GsPageHeader` | Standardized h1 + optional subtitle + right-side action slot | Tailwind |
| `GsDayIntentGrid` | Date-range × slot matrix for meal and chore plan views; coverage state per cell | `UTable` or CSS grid |
| `GsAccommodationCard` | Accommodation card with type, capacity, intent status | `UCard`, `GsStatusBadge` |

---

## 6. Pinia Stores (`app/stores/`)

**`useTenantStore`**
- State: `currentTenantId`, `currentTenantName`, `currentUserRole: TenantRole | null`
- Actions: `setTenant(id, name)`, `setUserRole(role)`
- Populated by `tenant.ts` middleware on all `/app/**` routes

**`useCurrentMemberStore`**
- State: `linkedMemberId: string | null`, `linkedHouseholdId: string | null`
- Used for self-edit access control and "Your profile" link in the account menu

**`useEventStore`**
- State: `activeEventId`, `activeEventDateRange: { start, end }`
- Avoids prop-drilling event context through the tabbed event detail page

---

## 7. Mobile-First Implementation Notes

- All page layouts use **mobile-first Tailwind breakpoints** (`md:`, `lg:`). Start from single-column and expand.
- Touch targets must be at least 44×44px (Nuxt UI's default button sizing handles this).
- Calendar on mobile: use FullCalendar `listWeek` view as the default (not `dayGridMonth`) — it renders as a readable list on narrow viewports. Provide a view toggle that remembers the user's preference per breakpoint if feasible.
- Tables that are too wide for mobile use horizontal scroll with `overflow-x-auto` wrapper, or collapse to a card-per-row layout.
- The Event Detail tab bar (`UTabs`) must scroll horizontally on mobile if all four tabs don't fit.
- Forms are already single-column — no extra work needed.
- The `GsDayIntentGrid` (meal/chore matrix) is the trickiest mobile case: it will likely need a separate mobile layout that stacks days vertically rather than showing a wide matrix.

---

## 8. Supporting Patterns

**Role composable**: `useTenantRole` wraps `useTenantStore` and exposes `isOwner`, `isManagerOrAbove`, `isMemberOrAbove` booleans. Used in `GsRoleGate` and `v-if` guards. Never use `v-show` for security-gated content.

**Security — client-side role state cannot escalate API privileges**: The Pinia role store is purely a UI rendering concern. The .NET API never trusts role values from the client. Every authorization decision uses a server-side DB lookup via `RequireTenantAccessAttribute` (reads `TenantUsers` table) and `MemberAuthorizationService` (reads `TenantUsers` and `HouseholdMembers`). The Nuxt proxy forwards only the Bearer JWT token, which contains only the user's external ID (`sub` claim) — no role claims. Tampering with Pinia state will break the UI but cannot elevate what the API will actually permit. The role stored in Pinia must be fetched from the API on tenant entry — it is display state, not an authorization token.

**Forms**: `UForm` + Zod schemas in `app/schemas/`. `UFormField name` prop wires automatic error display. All validation messages use i18n keys.

**API calls**: `$fetch('/api/proxy/tenants/{tenantId}/...')` through the existing proxy, where `tenantId` comes from `useTenantStore().currentTenantId`. The `useAsyncData` key must include the tenantId value to prevent cross-tenant cache collisions when a user switches groups.

**Toast feedback**: `useToast()` for all action results. Errors translated via existing `useApiError().translateError()`.

**FullCalendar**: Wrap in `<ClientOnly>` to be safe (all tenant routes are already client-only). Pass `useI18n().locale` to FullCalendar's `locale` option. `@fullcalendar/daygrid` and `@fullcalendar/list` packages must be added to `package.json`.

**i18n gaps to fill**: `en.json` needs new top-level keys for `directory`, `dashboard`, `attendance`, `meals`, `chores`, `accommodation`, `settings`, `reports`. Existing `nav`, `household`, `member`, `event`, `property` keys are already in place.

---

## 9. Implementation Phases

### Phase 1 — Foundation ✅ COMPLETE (2026-04-21)
1. ✅ `useTenantStore` + `useCurrentMemberStore` + `useEventStore` Pinia stores
2. ✅ `useTenantRole` composable
3. ✅ `app/middleware/tenant.global.ts` — global middleware scoped to `/app/**` routes: reads `last_tenant_id` cookie, populates Pinia store from API if empty, redirects to `/tenants` if no valid tenant found
4. ✅ Refactor `layouts/default.vue` — full sidebar nav with role gates, tenant name display, user account dropdown, mobile bottom tab bar
5. ✅ `GsPageHeader`, `GsEmptyState`, `GsConfirmModal`, `GsBreadcrumb`, `GsRoleGate`, `GsStatusBadge`

Backend changes shipped with Phase 1:
- ✅ `TenantSummary` extended with `TenantRole? UserRole`
- ✅ `TenantService.ListAsync` projection includes `tu.Role`
- ✅ `nuxt.config.ts` — `/app/**` route rule added (SSR disabled)
- ✅ `useTenants.ts` — fixed response unwrapping (`BaseEntityResponse` wrapper), added `userRole` field
- ✅ `i18n/locales/en.json` + `es.json` — added `nav.directory`, `nav.reports`, `nav.more`, `nav.yourProfile`, `nav.switchGroup`, `dashboard.*`, `status.*` keys

### Phase 2 — Core Member Value ✅ COMPLETE (2026-04-21)
6. ✅ `GsCalendar` component (FullCalendar wrapper) + installed `@fullcalendar/daygrid` and `@fullcalendar/list`
7. ✅ Dashboard (`app/index.vue`) — 3-column layout (upcoming events, listWeek calendar, tasks placeholder)
8. ✅ Event List (`events/index.vue`) — calendar (dayGridMonth) + list view toggle, persisted in localStorage
9. ✅ Event Detail (`events/[eventId]/index.vue`) — 4-tab layout; Overview tab with dayGridMonth calendar + per-day attendance strip (gated on linkedMemberId)
10. ✅ `GsAttendanceToggle` — Going/Maybe/NotGoing with per-button loading state

New composables shipped with Phase 2:
- ✅ `useEvents()` + `useEvent(eventId)` — tenant-scoped event fetch with reactive keys
- ✅ `useEventAttendance(eventId)` — fetch + upsert attendance records

### Phase 3 — Family Directory ✅ COMPLETE (2026-04-22)
11. ✅ Household List (`/app/directory/index.vue`) — search bar + card grid, role-gated create button
12. ✅ Household Detail (`/app/directory/[householdId]/index.vue`) — member list cards, breadcrumb
13. ✅ Member Detail (`/app/directory/[householdId]/[memberId]/index.vue`) — two-column identity + dietary profile, edit button gated to self or Manager+
14. ✅ Member Edit (`/app/directory/[householdId]/[memberId]/edit.vue`) — UForm with name, isAdult, ageBand, birthDate, dietaryNotes, dietaryTags; unsaved-changes guard via `onBeforeRouteLeave`
15. ✅ `GsMemberAvatar` — initials circle with xs/sm/md/lg sizes
16. ✅ `GsDietaryTags` — allergy chips (secondary/harvest), restriction chips (success/sage), basic tag chips (neutral)

New composables shipped with Phase 3:
- ✅ `useHouseholds()` + `useHousehold(householdId)` — tenant-scoped household fetch with reactive keys
- ✅ `useHouseholdMembers(householdId)` + `useMember(householdId, memberId)` + `useDietaryProfile(householdId, memberId)` — member and dietary profile fetch; dietary profile silently returns null on 404

i18n additions: `household.noHouseholds*`, `member.adult/child/identity/addMember/editMember/dietaryProfile/preferredDiet/allergies/restrictions/noDietaryProfile/dietaryTags*`, `common.notes/unsavedChanges`

### Phase 4 — Meal & Chore Plans ✅ COMPLETE (2026-04-22)
17. ✅ Meal Plan View — Meals tab in event detail replaced with live `GsMealTemplateSection` per template; `useMealTemplates(eventId)` fetches templates, `useMealPlanSection(eventId, templateId, memberId, householdId)` fetches plans + member intents in parallel (Promise.all per plan), upserts via PUT
18. ✅ Chore Plan View — Chores tab replaced with live `GsChoreTemplateSection`; `useChoreTemplates(eventId)` + `useChorePlanSection(…)` follow same pattern; volunteer toggle (boolean) replaces status enum
19. ✅ `GsDayIntentGrid` — desktop scrollable table + mobile day-stacked layout via `sm:` breakpoint; generic scoped `#cell` slot so parent controls cell content

New composables shipped with Phase 4:
- ✅ `useMealPlans.ts` — `useMealTemplates(eventId)`, `useMealPlanSection(eventId, templateId, memberId, householdId)`; exports `MealTemplate`, `MealPlan`, `MealIntent`, `MealIntentStatus`, `mealTypesFromFlags(flags)`
- ✅ `useChoreTemplates.ts` — `useChoreTemplates(eventId)`, `useChorePlanSection(eventId, templateId, memberId, householdId)`; exports `ChoreTemplate`, `ChorePlan`, `ChoreIntent`, `choreSlotsFromFlags(flags)`

New components shipped with Phase 4:
- ✅ `GsDayIntentGrid.vue` — generic `rows/columns` props, `#cell` scoped slot; desktop table / mobile stack
- ✅ `GsMealTemplateSection.vue` — UCard per template; fetches plans + member intents; renders `GsDayIntentGrid` with `GsAttendanceToggle` (reuses Going/Maybe/NotGoing — same enum values as `MealIntentStatus`)
- ✅ `GsChoreTemplateSection.vue` — UCard per template; fetches plans + member intents; renders `GsDayIntentGrid` with Sign Up / Signed Up `UButton` (success color when volunteered)

i18n additions: `event.meal.{breakfast,lunch,dinner,noTemplates,noPlans}`, `event.chore.{morning,midday,evening,anytime,volunteer,volunteered,noTemplates,noPlans,minimumAssignees}` (en + es)

### Data Layer Refactor (planned, before or alongside Phase 5)
See **[REPOSITORY-PATTERN.md](REPOSITORY-PATTERN.md)** for the full plan. Summary:
- Introduce `app/repositories/` with live (`$fetch`-backed) and demo (localStorage-backed) implementations per domain aggregate
- Composables become thin `useAsyncData` wrappers; all `if (demoMode)` branches removed
- A `.client.ts` Nuxt plugin selects the correct implementation at startup via `provide/inject`
- Demo is fully interactive and localStorage-persisted, with conversion-gate entity limits surfaced as toasts
- Fixes `tenant.global.ts` bug where demo mode still calls the proxy API
- Adds a `UAlert` demo warning banner to `default.vue`

### Phase 5 — Properties & Accommodations
17. Property List + Property Detail + accommodation grid
18. Accommodation Intent Management
19. `GsAccommodationCard`

### Phase 6 — Management & Settings
20. Full Attendance Manager grid (Manager+)
21. Dietary Aggregation view
22. Settings hub + roles page + danger zone

---

## 10. Open Design Questions (resolved assumptions)

- **Properties in Member nav?** Yes — members need to see accommodation status. They'll see Properties in nav but the page will show only their own accommodation intent; Manager controls are hidden via `GsRoleGate`.
- **Dashboard vs. Events calendar** — Dashboard is "at-a-glance" with a compact calendar widget; Events is the full-featured calendar. They serve different depths of the same information.
- **Tenant name in sidebar** — Small muted text below the logo (not a badge), so users know which group they're in without making switching prominent.

---

## 11. Verification

After implementation:
1. **Build check**: `dotnet build Gatherstead.sln` (0 errors, 0 warnings) — backend unchanged
2. **Frontend build**: `pnpm build` from `src/Gatherstead.Web/` — no type errors
3. **Lint**: `pnpm run lint` from `src/Gatherstead.Web/`
4. **Manual walk-through** (with `demoMode: true` or a dev backend):
   - Sign in → lands on `/app` dashboard
   - Desktop: sidebar shows role-gated items; mobile: bottom tab bar renders
   - User dropdown → "Switch group" navigates to `/tenants`
   - Navigate to Events → calendar renders with coverage status colors; click event → tabbed detail
   - Navigate to Directory → household list → member detail → dietary tags visible
   - Navigate to Properties → accommodation card grid with intent status badges
   - As Member: Settings link absent; Manager+ controls not visible; status icons/colors render correctly
   - As Manager: Settings link present, create/edit buttons visible, coverage badges on meal/chore grids

# Web UI Design Guidelines

Durable frontend UX/UI conventions for the Gatherstead web app (Nuxt 4 + Vue 3 + Nuxt UI v4 + Tailwind). These are the patterns every page and component must follow — distilled from the original design plan in [agents/plans/WEB-UI-DESIGN.md](agents/plans/WEB-UI-DESIGN.md), which retains the phased implementation history and route map.

For data-access, tenancy, and authorization rules see [ARCHITECTURE.md](ARCHITECTURE.md) (Frontend conventions) and [DESIGN_PRINCIPLES.md](DESIGN_PRINCIPLES.md). This document covers presentation and interaction.

## North Stars

- **Mobile-first.** A high fraction of users are on phones. Start every layout single-column and expand with `md:`/`lg:` Tailwind breakpoints — never the reverse. Touch targets ≥ 44×44px (Nuxt UI's default button sizing satisfies this). Wide tables get an `overflow-x-auto` wrapper or collapse to card-per-row; wide matrices (e.g. `GsDayIntentGrid`) get a stacked mobile layout.
- **Clean for members, progressively richer for managers.** Default views are simple; management controls are additively revealed by role, never crowding the member experience.
- **Calendar is the primary information surface** for event/attendance data.
- **Tenant switching stays tucked away** (account dropdown → "Switch group"), never prominent in the main nav.

## Component Vocabulary — prefer the shared `Gs*` components

Reach for an existing component before hand-rolling markup. Building a new page means *composing* these, not reinventing them.

| Need | Use | Notes |
|---|---|---|
| Page title + actions | `GsPageHeader` | `title` (+ optional `description`); right-side action `slot`. Every top-level page starts with one. |
| Breadcrumb | `GsBreadcrumb` | Tenant-aware; starts at the tenant/section root. |
| Empty / first-run state | `GsEmptyState` | `icon` + `title` + optional `description` + action `slot`. Use for "no data", "no access", and not-found states — not bare `<p>` tags. |
| Destructive confirm | `GsConfirmModal` | `danger` for deletes; describe exactly what is destroyed. |
| Any domain status | `GsStatusBadge` | **The single source** of status→color→icon mapping (see Visual Status Language). Never inline a status color/icon. |
| Role-gated UI | `GsRoleGate min-role="…"` | Renderless; renders slot only when the role floor is met. |
| Attendance toggle | `GsAttendanceToggle` | Going/Maybe/NotGoing tri-toggle. |
| Dietary chips | `GsDietaryTags` | Allergy (`secondary`) + restriction (`success`) chips. |
| Member initials | `GsMemberAvatar` | Stackable for groups. |
| Calendar | `GsCalendar` | FullCalendar wrapper; locale-bound, wrapped in `<ClientOnly>`. |
| Date sub-range field | `GsTemplateDateRangeField` | Toggle + start/end date pair for template modals. |

Prefer **Nuxt UI** primitives (`UButton`, `UInput`, `UCard`, `UModal`, `UFormField`, `UBadge`, …) over native HTML elements wherever an equivalent exists.

## Reuse & Componentization

The default is to reuse, not duplicate. Before adding a page, component, or helper, look for an existing one to compose or extend.

- **One component for create *and* edit.** An entity's create and edit UI is a single component, not two near-identical copies. Pass the entity to switch to edit mode and omit it to create (`GsHouseholdModal`, `GsPropertyModal`: an optional `household`/`property` prop drives `isEdit`, the title, the submit label, and pre-fill). For modals, pre-fill on open via `watch(open, …)`; for pages, pre-fill from the loaded entity via `watch(entity, …, { immediate: true })`.
- **Extract a shared form component when create and edit share fields.** Multi-field forms whose fields are identical across create and edit live in one `Gs*Form` component (`GsMemberForm`) that the create page and edit page both render. Bind each field with `defineModel`; surface `:error`/`:loading`/`submitLabel`/`cancelTo` as props and `submit`/clear-error as emits. Page-specific concerns (pre-fill, the `isDirty` unsaved-changes guard, navigation) stay in the page, not the shared form.
- **Factor repeated logic into a composable or util.** A helper duplicated across two or more files (e.g. date formatting, a derived label, a mapping) moves into `app/composables/` or `app/utils/` and is imported. Don't copy-paste it.
- **Keep frontend/backend conventions mirrored.** When a mapping or rule exists on the server (e.g. meal-type → task-slot flags), mirror it in one shared frontend helper rather than re-deriving it inline per call site.

## Visual Status Language

A consistent icon + color system so users parse status at a glance. **Color is never the only differentiator** (accessibility) — always pair it with an icon and a label. Every status render goes through `GsStatusBadge`; if a status isn't covered, extend the component's `statusMap` rather than styling inline.

Color tokens (from `app.config.ts`): `primary=forest`, `secondary=harvest`, `success=sage`, `neutral=warmstone`, `error`.

- **Coverage** (MealPlan/TaskPlan): Covered=`success`/check-circle · Partial=`secondary`/clock · Open=`neutral`/question-mark-circle · Exception=`primary`/x-circle.
- **Attendance** (EventAttendance/MealIntent/TaskIntent): Going=`success`/check · Maybe=`secondary`/question-mark · NotGoing=`neutral`/x-mark (muted — absence is not an error) · NoResponse=`neutral` faded/minus.
- **Accommodation intent**: Intent=`neutral`/hand-raised · Hold=`secondary`/pause-circle · Confirmed=`success`/lock-closed · Declined=`error`/x-circle.

## Interaction Patterns

- **Create vs. Add nomenclature.** "Create [Entity]" creates something new (Create Household, Create Event). "Add" is reserved for associating an *existing* thing (Add Household Access). Don't mix them.
- **Create/edit surface choice.** Single-field entities (Household, Property) use a `UModal` opened from the list or detail page — the modal takes a `refresh` prop and calls `useXxxActions(refresh)`, and the *same* modal handles edit (see Reuse & Componentization). Multi-field entities (Event, Member) use a dedicated route page with a form.
- **Gate the next step on success — never assume it.** Mutating actions in `useXxxActions` (create *and* update) catch errors, surface a toast, and **return a boolean** (or the entity / `null`). The caller only advances on success: a modal does `if (ok) open.value = false`; a page does `if (ok) await navigateTo(…)`. A validation/server failure keeps the modal open or the user on the form with their input intact. Never close-or-navigate-then-discover-failure.
- **Destructive deletes always confirm.** Route every delete through `GsConfirmModal` with `danger` and a body that names what will be destroyed. No entity is deleted on a single click.
- **Toast feedback for every action result.** Use `useToast()`; translate errors via `useApiError().translateError()`. Demo-limit hits surface a friendly warning toast.

## Role Gating

- Gate management controls with `GsRoleGate min-role="…"` or the `useTenantRole` booleans (`isOwner`, `isManagerOrAbove`, `isMemberOrAbove`). Use `v-if` (the gate is renderless) — **never `v-show`** for security-relevant content.
- Client role state is **display-only**. The .NET API independently authorizes every request server-side; Pinia role tampering breaks the UI but cannot escalate privileges. Always also expect the API to enforce access (a deep-linked page should degrade to a clear "no access" `GsEmptyState`, not assume the gate held).

## Forms

- Build with `UForm` + `UFormField`; the `name` prop wires automatic error display. Bind a per-field `:error` and clear it on input for inline validation.
- All validation messages use i18n keys (`validation.required`, etc.).
- Warn on unsaved changes when navigating away from a dirty multi-field form (`onBeforeRouteLeave` + confirm).
- When create and edit pages share the same fields, extract the form into one `Gs*Form` component they both render (see Reuse & Componentization) rather than maintaining two copies.

## Data, i18n, and SSR

- **Data access only through `useRepositories()` composables** — never `$fetch` directly in a page/component. (The sole sanctioned exception is the `tenant.global.ts` bootstrap.) See ARCHITECTURE.md.
- **Every user-visible string goes through i18n** (`useI18n().t()`); keep `en.json` and `es.json` in exact key parity. Escape literal `@` in placeholders as `{'@'}` (vue-i18n treats `@` as linked-message syntax). Build tab/label arrays as `computed` so they re-translate on locale switch.
- **Format dates/numbers from the active locale.** Use the shared `useFormatDate()` composable (or an equivalent `Intl` helper) that reads `useI18n().locale` — never pass `undefined` to `Intl.DateTimeFormat`/`NumberFormat` (that silently falls back to the browser default and ignores the user's chosen language). Don't redefine `formatDate`/`formatDay` per page.
- Authenticated routes are client-only SPA (`/app/**` SSR disabled); wrap `GsCalendar`/FullCalendar in `<ClientOnly>`.

## Checklist for a new or edited page/component

1. Starts with `GsPageHeader` (+ `GsBreadcrumb` for sub-pages); empty/error/not-found states use `GsEmptyState`.
2. All statuses render via `GsStatusBadge` — no inline status colors/icons.
3. Management actions wrapped in `GsRoleGate`; deep-link degrades gracefully.
4. Deletes use `GsConfirmModal` (`danger`, descriptive body); modals close / pages navigate only on a successful (`true`) action result.
5. "Create" vs "Add" used correctly; create UX matches the single-field-modal / multi-field-page rule.
6. Create and edit reuse one component (entity-prop drives edit mode); shared multi-field forms live in a `Gs*Form`; no duplicated logic that belongs in a composable/util.
7. Data via repository composables; results/errors surface via toast.
8. Every string is an i18n key (en + es parity); locale-reactive labels are `computed`; dates/numbers formatted via the locale-aware helper, not `undefined`.
9. Mobile-first layout verified at a narrow viewport; touch targets adequate; wide tables/grids handled.
10. `pnpm run lint` and `pnpm build` clean.

---
updated: 2026-07-10
commit: dcf7863
status: design — not yet implemented
---

# Guest Access — Design

## Summary

A **Guest** is an authenticated user with `TenantRole.Guest` whose person-record lives in a hidden per-tenant system household and whose visibility is positively scoped to events they hold an `EventInvitee` row for. Guests are true outsiders — friends, partners, occasional visitors — who participate in specific gatherings without joining the family directory.

Core rules:

- Guests authenticate through the normal Entra / `Invitation` claim flow. **Login is never blocked**; a guest with no upcoming invited events simply sees an empty "no upcoming invitations" state. Access duration is a presentation rule driven by event invitations, not an account lifecycle.
- Within an invited event a guest can: respond to daily attendance (`EventAttendance`), respond to meals (`MealAttendance`), volunteer for tasks (`TaskIntent`), request accommodation stays (`AccommodationIntent`), and claim event-scoped shopping items (`ShoppingItemIntent`). Cook sign-up (`MealIntent`) is blocked — it carries meal-plan menu-edit privileges.
- Guests can never browse the Directory, Properties, Equipment, Reports, or Settings.
- **This design replaces the existing household-linked guest capability entirely** (an `Invitation` with `Role=Guest` + `HouseholdId` granting `SensitiveReadScope.ForHouseholds`). That flavor is latent plumbing with no dedicated UX; retiring it keeps one guest mental model. The caregiver/household-helper case is deferred and can be reintroduced deliberately if a real need appears.

Why this shape: every participation table already keys to `HouseholdMemberId` (NOT NULL), and `HouseholdMember.HouseholdId` is NOT NULL. Housing guests in a flagged system household reuses all existing FK plumbing — attendance grids, headcounts, dietary tallies, and reports include guests with zero schema change to participation tables — and makes promotion to full member a simple row move with history intact.

## Current-State Gaps This Closes

- `TenantRole.Guest` exists and is wired through role checks and `SensitiveReadScope`, but **reads are ungated**: a Guest today can list all Events, Properties, Equipment, Accommodations, Shopping items, and the directory's public member fields (name, age band).
- There is no event-invitation concept — `EventAttendance` records responses only; "invited but not responded" is unrepresentable.
- The invitation flow creates `TenantUser`/`HouseholdUser` rows but never a `HouseholdMember`, so an invited guest has no person-record to RSVP or claim with (frontend claim flows require `linkedMemberId`).
- The frontend nav and routes are not role-gated: guests see Directory/Properties/Accommodations/Equipment/Events/Shopping navigation; only Reports (Member+) and Settings (Manager+) are gated.

## Settled Decisions

1. Guest person-record: **hidden system household** — one flagged "Guests" household per tenant; directory excludes it.
2. "Invited to an Event" = new **`EventInvitee`** join entity; a guest's visible event set = their invitee rows.
3. Guest participation scope: daily RSVP, meal attendance, task volunteering, accommodation requests, shopping claims. **`MealIntent` cook sign-up blocked** for guests.
4. **Plus-ones are named people**: one `HouseholdMember` per attending person; no anonymous party counts on `EventAttendance`. (`AccommodationIntent.PartyAdults/PartyChildren` stays as-is — it is informational occupancy, not attendance.)
5. Guests see **participant names** (name + age band only) for their invited events via a new event-scoped participants endpoint — never contacts, dietary data, or household structure.
6. **Past invited events stay readable indefinitely** (read-only, collapsed "Past" group in the UI); the empty state keys off upcoming events only.
7. **Household-linked guest access is retired** (see Summary). All `Role=Guest` invitations reject `HouseholdId`/`HouseholdRole` with a validation error. `SensitiveReadScope` simplifies to `Global` / `Self` / `None`.
8. Promotion/demotion between Guest and Member is a supported lifecycle pathway (documented below), deferred to the household-migration workflow — no new schema needed in either direction.

## Data Model

### `HouseholdType` on `Household`

```csharp
// Enums.cs
public enum HouseholdType
{
    Standard = 0,
    Guests   = 1,   // hidden system household; one per tenant
}
```

- `Household` gains `Type` (default `Standard`). Filtered unique index enforces one live system household per tenant, configured fluently in `GathersteadDbContext` (attribute syntax cannot express filters), mirroring the existing pending-invitation filtered index: `HasIndex(TenantId).IsUnique().HasFilter("[Type] = 1 AND [IsDeleted] = 0")`.
- **Creation is lazy**, on first guest onboarding, via `GuestProvisioning.GetOrCreateGuestHouseholdAsync(dbContext, tenantId, ct)` — a static helper sibling of `MembershipGrant` in `Services/Membership/`. Default name `"Guests"`; on collision with an existing household of that name (the plain `(TenantId, Name)` unique index spans soft-deleted rows), fall back to `"Guests (System)"` then GUID-suffixed — cosmetic only, since the system household is never presented as a household in the UI. The filtered unique index makes concurrent creation race-safe: catch the unique violation and re-query.
- **Protection**: `HouseholdService` update/delete reject `Type == Guests` households (cannot rename, delete, or merge). Creating members inside it is restricted to the guest-onboarding path — not the general member-create endpoint.
- `HouseholdDto` gains `Type` (string enum) so the frontend can filter it out of the directory and household pickers.

### `EventInvitee` (new entity)

```csharp
[Index(nameof(TenantId), nameof(EventId), nameof(HouseholdMemberId), IsUnique = true)]
[Index(nameof(TenantId), nameof(HouseholdMemberId))]   // guest-scope resolution
public class EventInvitee : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid HouseholdMemberId { get; set; }
    public Guid? InvitedByUserId { get; set; }      // audit, mirrors Invitation.InvitedByUserId
    [MaxLength(500)] public string? Notes { get; set; }
    // + Tenant/Event/HouseholdMember navs; Event gains ICollection<EventInvitee> Invitees
}
```

- Soft-delete via `AuditableEntity` + the composable global query filters, like every entity. Re-invite after revoke uses the find-or-revive pattern from `ShoppingItemService.UpsertIntentAsync` (the unique index spans soft-deleted rows).
- No PII columns — ids are safe to log.
- Deliberately **member-generic**, not guest-only: any `HouseholdMember` can be an invitee. Today it only gates guests; later it can back invite-only events for members without schema change.

### `Invitation` extension — one-step guest invite

`Invitation` gains two nullable FKs (no PII — the guest's name goes straight into the Always-Encrypted `HouseholdMember.Name`):

```csharp
public Guid? EventId { get; set; }         // set only when Role == Guest
public Guid? GuestMemberId { get; set; }   // HouseholdMember materialized at create time
```

`CreateInvitationRequest` gains transient `EventId` and `GuestName` fields (name defaults to the email local-part when absent).

**Guest branch in `InvitationService.CreateAsync`** (when `Role == Guest && EventId != null`):

1. **Authorization**: `AuthorizeEventManageAsync` (Coordinator+) — coordinators run events, so they invite guests. `RequireNonEscalatingRole` is trivially satisfied (Guest = 4). Validate the event exists in-tenant. Per decision 7, **any** `Role=Guest` request carrying `HouseholdId`/`HouseholdRole` is rejected with a validation error.
2. **Materialize the person immediately** (before any login): `GetOrCreateGuestHouseholdAsync`, then resolve-or-create the guest `HouseholdMember`:
   - a `User` with this email exists with a `TenantUser.LinkedMemberId` in this tenant → reuse that member;
   - else a prior `Invitation` in this tenant for this email carries `GuestMemberId` (any status; check soft-deleted/revoked via `IgnoreQueryFilters` on soft-delete) → reuse that member (returning guest who never logged in);
   - else create a new `HouseholdMember` in the Guests household with `Name = GuestName`.
3. **Materialize the `EventInvitee`** (find-or-revive on the unique key).
4. **Invitation row**: if a Pending invitation for this email already exists (filtered unique index), do not create a second — add the `EventInvitee` against the existing `GuestMemberId` and return the existing invitation. Otherwise create with `EventId`/`GuestMemberId`; if the `User` already exists, grant immediately as today.
5. **Grant links the member**: `MembershipGrant.GrantAsync` gains an optional `Guid? linkMemberId` — sets `TenantUser.LinkedMemberId` when creating the row, or only-if-currently-null on an existing row (never overwrite a self-link). Both call sites (immediate-accept in `InvitationService`, bootstrap claim in `UserProvisioningService.ClaimInvitationsAsync`) pass `invite.GuestMemberId`. This is the single place a guest acquires a person-record link; the bootstrap flow needs no other change.

**Materialize-at-create is the key UX property**: the guest appears in attendance grids, headcounts, and dietary tallies the moment they are invited, and coordinators can RSVP/plan on their behalf before the guest ever logs in — exactly how member records already behave.

**Returning guest** (same email, second event a year later): step 2 resolves the existing member; only a new/revived `EventInvitee` is added; `MembershipGrant` is add-if-absent so the existing Guest `TenantUser` is untouched. All history stays attached to the same member id.

**Migrations**: `Household.Type` + filtered index + `EventInvitees` table + `Invitation.EventId`/`GuestMemberId` (one or two EF migrations). No new Always-Encrypted columns — guest `Name` reuses the existing encrypted-column configuration.

## Authorization Model

### `EventParticipationScope` (new, analogous to `SensitiveReadScope`)

```csharp
public sealed class EventParticipationScope
{
    public static readonly EventParticipationScope Global;   // Member+ (role <= Member) or App Admin
    public static EventParticipationScope ForEvents(
        IReadOnlyCollection<(Guid EventId, Guid PropertyId)> invited); // Guest with invitee rows
    public static readonly EventParticipationScope None;     // Guest with none

    public bool IsGlobal { get; }
    public bool CanAccessEvent(Guid eventId);
    public bool CanAccessProperty(Guid propertyId);  // property hosting any invited event
    public IReadOnlySet<Guid>? EventIds { get; }     // null when Global — drives positive list filtering
}
```

Resolved by `IMemberAuthorizationService.GetEventScopeAsync(tenantId, ct)`:

- App Admin or `TenantRole <= Member` → `Global` (short-circuits on the already-cached role lookup; no extra query for members).
- Guest: one indexed query — `EventInvitees` for the caller's `LinkedMemberId`, selecting `(EventId, Event.PropertyId)` → `ForEvents(...)` or `None`.
- **Includes past invited events** — the future-only rule is presentation (frontend grouping/empty state), never authorization.
- Cached per-request in `HttpContext.Items` (same idiom as the tenant-role cache). Not placed in `IAuthCache`/HybridCache initially: invitee sets change on invite/revoke and cross-request eviction adds complexity for a single indexed per-request query. Future optimization: cache with eviction hooks in the invitee write paths.

### `SensitiveReadScope` simplification

Per decision 7, `ForHouseholds` is removed. The scope becomes:

- `Global` — Member+ or App Admin (unchanged);
- `Self` — new: the caller's own `LinkedMemberId` is always sensitively readable, carried as `SelfMemberId` on the scope;
- `None` — everything else.

The `Self` addition fixes an existing asymmetry: a guest can *write* their own `BirthDate`/`DietaryNotes`/`DietaryTags` (Self path in `CanEditMemberAsync`) but `SensitiveReadScope.None` masks those same fields on read and 403s their own dietary-profile endpoints. Guests must maintain their own dietary info — that is much of the point of tracking them. Call sites (`HouseholdMemberService.ListAsync`/`GetAsync` masking, `ServiceGuards.AuthorizeSensitiveReadAsync`, sub-entity endpoints for Addresses/ContactMethods/DietaryProfile) take a memberId-aware overload. Deliberately **not** solved by granting the system household to `ForHouseholds` — that would expose other guests to each other.

Rollout note: audit for pre-existing Guest-role `HouseholdUser` rows before removing the scope (expected: none in prod; clean up if found).

### New `ServiceGuards`

Following the composable guard idiom (mutate `BaseEntityResponse<T>` on failure, return `false`):

```csharp
AuthorizeEventReadAsync(response, authz, tenantId, eventId, ct)
    // scope.CanAccessEvent(eventId) else PERMISSION_EVENT_READ
AuthorizePropertyScopedReadAsync(response, authz, tenantId, propertyId, ct)
    // scope.IsGlobal || scope.CanAccessProperty(propertyId)
AuthorizeMemberRoleReadAsync(response, authz, tenantId, ct)
    // scope.IsGlobal — the hard "Member+ only" gate (Equipment, Properties list, directory)
```

Plus new `ErrorCode` values (`PERMISSION_EVENT_READ`, `PERMISSION_MEMBER_READ`). Validation ordering stays: tenant context → request → authorization → normalization → existence → load. `RequireTenantAccessAttribute` remains the coarse controller gate; add `MinimumRole = TenantRole.Member` at the controller level only where an entire controller is Member+ (Equipment). List endpoints use **positive filtering** (guests receive *their* data, not errors); single-resource endpoints return 403 via the guards.

### Guest read/write matrix

| Resource | Guest read | Guest write | Enforcement |
|---|---|---|---|
| Events | Invited events only (positive filter on `scope.EventIds`) | No (Coordinator+ unchanged) | `EventService.ListAsync` filter; `GetAsync` → `AuthorizeEventReadAsync` |
| EventAttendance | Invited events only | Own row only (existing Self path) | reads gain `AuthorizeEventReadAsync`; writes unchanged |
| MealTemplates / MealPlans | Invited events only | No | `AuthorizeEventReadAsync` (eventId on route) |
| MealAttendance | Invited events only | Own row only (existing Self path) | reads event-gated; writes unchanged |
| MealIntent (cook sign-up) | Invited events only | **No — explicit deny**: cook sign-up grants menu-edit (`CanEditMealPlanMenuAsync`); deny in `MealIntentService.UpsertAsync` when caller role is Guest | `MealIntentService` |
| TaskTemplates / TaskPlans | Invited events only | Plan updates: no | `AuthorizeEventReadAsync` |
| TaskIntent (volunteering) | Invited events only | **Yes, self** (existing Self path) | reads event-gated; writes unchanged |
| Accommodations (inventory) | Only properties hosting an invited event | No | `AuthorizePropertyScopedReadAsync` |
| AccommodationIntent | In-scope properties only (occupancy visibility to pick a free spot) | **Yes, self** — own `Requested` stays at in-scope properties; status transitions beyond `Requested` remain Coordinator+ | reads property-scoped; writes: existing Self check + property-scope check for Guests |
| ShoppingItems | Items with `EventId ∈ scope.EventIds` (meal-origin items carry denormalized `EventId` — covered); property-origin items hidden | Item CRUD: no. **Intents (claim/provide/unclaim): yes** — own member, in-scope items only | `ListAsync` positive filter; `UpsertIntentAsync`/`DeleteIntentAsync`: Guest ⇒ require `item.EventId != null && scope.CanAccessEvent(item.EventId)` and `memberId == LinkedMemberId` |
| Properties | List: no. Get: in-scope properties only, trimmed to name/notes (no address column exists; attributes already role-gated via `TenantMinRole`) | No | list `AuthorizeMemberRoleReadAsync`; get `AuthorizePropertyScopedReadAsync` |
| Equipment | No | No | controller `MinimumRole = Member` |
| Households | No (empty list; Guests household never listed to guests) | No | `HouseholdService.ListAsync` filter for Guest callers |
| HouseholdMembers | Own linked member row only (full, incl. sensitive via `Self`); no directory fan-out | Own row (existing Self path) — guests maintain their own name/dietary data | List/Get guest gating; sub-entities use memberId-aware sensitive guard |
| Event participants (new) | Invited events only | — | `AuthorizeEventReadAsync` |
| Reports | No (already Member+ via `AuthorizeGlobalSensitiveReadAsync`); guest *data* appears in members' reports automatically | — | unchanged |
| Settings / TenantUsers / Invitations / SecurityEvents | No (Manager+ already) | No | unchanged |
| DietaryTags catalog / lookups | Yes (needed for own-profile editing) | — | unchanged |

### Event participants endpoint

Guests cannot fan out `useAllMembers()` (directory blocked), yet their event pages must render names on attendance/meal/task/shopping surfaces. Add a minimal event-scoped projection:

`GET /tenants/{tenantId}/events/{eventId}/participants` → `EventParticipantDto(Guid MemberId, string Name, AgeBand? AgeBand, bool IsGuest)`

Union of (a) members holding any `EventAttendance` row on the event and (b) `EventInvitee` members. Guarded by `AuthorizeEventReadAsync`. Returns names only — no contacts, dietary data, birth dates, or household ids. Rationale: people attending a shared gathering seeing each other's names is inherent to attending. Members' UIs keep using `useAllMembers`; guest UIs (and the members' Guests section) use participants. Implemented as a service method on `EventService` exposed from `EventsController`.

## Members' View & Guest Management

- **Grids, headcounts, dietary tallies, reports**: guest `HouseholdMember` rows flow through every member-keyed query automatically — no changes. Member+ callers retain `Global` sensitive scope, so cooks see guest dietary tags (intended: allergy safety).
- `HouseholdService.ListAsync` returns the Guests household **to Member+ callers** with `Type: "Guests"` so `useAllMembers()` keeps working and grids grow guest lanes. The Directory page and all household pickers filter `type === 'Guests'` client-side.
- **Management surface** — a "Guests" section on the event detail page (`pages/app/events/[eventId]/index.vue`), Coordinator+ via `GsRoleGate`:
  - **Invite guest** modal (email + name + optional note) → `POST /tenants/{t}/events/{e}/invitees` (`{ email?, guestName?, memberId? }` — email path for new guests; memberId path re-invites an existing guest member to another event). Backed by `EventInviteeService`, which delegates the email path to the `InvitationService` guest branch (single materialization implementation).
  - **Invitee list** (`GET .../invitees`, Coordinator+): name, invitation status (Pending = hasn't logged in yet / Accepted), RSVP summary from `EventAttendance`, invited-by, invited-at.
  - **Revoke** (`DELETE .../invitees/{inviteeId}`, Coordinator+): cascade below.
- Tenant settings → users already lists Guest-role `TenantUser`s; the per-event surface is the primary management UX.

## Frontend

- **Route gating**: global middleware (extend `tenant.global.ts` or a sibling `role.global.ts` running after it). When `currentUserRole === 'Guest'`, redirect `/app/directory`, `/app/properties`, `/app/accommodations`, `/app/equipment`, `/app/shopping`, `/app/reports`, `/app/settings` → `/app`. Guests keep `/app` and `/app/events/**`. Middleware is the right seam — role is already resolved there and it covers deep links; `GsRoleGate` continues gating intra-page elements.
- **Nav trimming** (`layouts/default.vue`): make `primaryNavItems`/`mobileNavItems`/`mobileMoreItems` role-aware via `useTenantRole().isMemberOrAbove` — guests see Dashboard + Events only. Replace the directory-routed "Your Profile" link with a guest-safe own-profile page (`/app/profile`, reads/edits own member via the Self-authorized endpoints — useful for everyone, not just guests).
- **Guest landing** (`/app`): "Your invitations" — the events list is already server-filtered to invited events; split upcoming (`endDate >= today`) vs a collapsed "Past" group; empty state (`dashboard.guest.empty.*`) when no upcoming: the "nothing state" that implements the access-duration rule.
- **Event page guest variant**: management controls already hide behind coordinator checks; guest sees self-only per-day RSVP toggles, self-only meal attendance, task volunteer toggle, "request a stay" accommodation flow (inventory for the event's property), and the event shopping list with claim buttons. Names resolve via a new `useEventParticipants()` composable (guests) while members keep `useAllMembers`.
- **Member store**: no change — `tenant.global.ts` hydrates `linkedMemberId`/`linkedHouseholdId` from `/tenants/{id}/users/me`, and `MembershipGrant` now sets `LinkedMemberId` at grant, so `useShoppingList.claim()`'s `linkedMemberId` requirement is satisfied for guests.
- **Demo parity**: `DemoStore` gains `eventInvitees`; demo repositories add invitee CRUD, participants derivation, and the same list-filtering rules; `seedDemoData.ts` seeds the Guests system household plus one guest member with invitee + attendance rows so members' grids/reports demonstrate guests.
- **i18n**: new key groups in `en.json`/`es.json`: `guests.*` (invite modal, invitee list, revoke confirm, status labels), `dashboard.guest.*`, `events.participants.*`. No hardcoded strings.
- **OpenAPI**: regenerate via `scripts/generate-openapi.sh` after each backend phase (new DTOs: `EventInviteeDto`, `EventParticipantDto`, `HouseholdDto.Type`, invitation request fields).

## Lifecycle & Edge Cases

- **Event ends**: API scope keeps past invited events readable indefinitely (writes to past-event data aren't blocked for anyone today — no special casing). The frontend collapses them under "Past" and drives the empty state off upcoming only. Guests are never auto-removed.
- **Invite revoked**: soft-delete the `EventInvitee`, then cascade (all soft-delete, preserving records per audit principles): the guest's `EventAttendance` + `MealAttendance` rows for that event, `TaskIntent` rows on that event's plans, `ShoppingItemIntent` rows on that event's items; `AccommodationIntent`s overlapping the event window at the event's property → `Declined` + soft-deleted. Keeps live headcounts/coverage/claims accurate (a revoked guest must not hold a claim or a hold) while retaining everything for Manager+ review. If the guest's `Invitation` is still Pending and no other live `EventInvitee` rows exist for the member, also revoke the invitation. The `TenantUser` (if any) is left alone — login stays possible; the app shows the empty state.
- **Promotion / demotion** (supported pathway; deferred to the household-migration workflow — no new schema either direction):
  - **Guest → Member** (e.g. marriage): update `TenantUser.Role`, move the `HouseholdMember` from the Guests household into a real household. **All history follows for free** — every participation row is member-keyed. Their `EventInvitee` rows become inert (Member+ scope is `Global`).
  - **Member → Guest** (e.g. divorce, moved away): downgrade `TenantUser.Role`, move the member row into the Guests household, remove their `HouseholdUser` grants; access thereafter is driven solely by event invitations. History again stays attached.
- **"Guests" name collision**: handled by creation-fallback naming; the filtered unique index keys on `Type`, not name.
- **One system household per tenant**: the filtered unique index; the creation helper catches the race.
- **Encrypted PII**: guest names reuse the existing Always-Encrypted `HouseholdMember.Name` column — zero Data.Setup changes. Guest name/email never appear in logs or security-event detail (ids only), matching existing invitation logging discipline.
- **Second event invited while first invitation still Pending**: no second `Invitation` row (filtered unique index); a second `EventInvitee` is added against the same `GuestMemberId`; the single claim at first login grants membership and both events are in scope.
- **Scope staleness**: `EventParticipationScope` is per-request, so invite/revoke take effect on the guest's next request. Tenant-role caching and its eviction paths are unchanged.

## Phasing

Each phase is independently shippable.

**Phase 1 — Schema + guest plumbing (no behavior change)**
`Enums.cs` (`HouseholdType`), `Household.cs`, `EventInvitee.cs`, `GathersteadDbContext` (DbSet, filters, filtered unique indexes), `Invitation.cs` fields, migrations; `MembershipGrant.GrantAsync` `linkMemberId` param; `GuestProvisioning` helper; `HouseholdDto.Type`.
Tests (`tests/Gatherstead.Data.Tests`, `tests/Gatherstead.Api.Tests`): index/filter behavior, grant-links-member idempotency (existing link never overwritten), one-system-household race.

**Phase 2 — Backend lockdown + scope (closes today's read gap; safe alone — guests see nothing until invited)**
`EventParticipationScope` + `GetEventScopeAsync`; `SensitiveReadScope` simplification (retire `ForHouseholds`, add `Self`; audit/clean pre-existing Guest `HouseholdUser` rows); new `ServiceGuards` + `ErrorCode`s; gates per the matrix across Event/EventAttendance/Meal*/Task*/Accommodation*/ShoppingItem/Property/Equipment/Household/HouseholdMember services; `MealIntent` guest deny; Guest-invitation `HouseholdId` rejection; participants endpoint + DTO.
Centerpiece test asset: an authorization-matrix integration suite (guest with/without invitee rows against every gated endpoint).

**Phase 3 — One-step guest invitation flow**
`InvitationService.CreateAsync` guest branch (Coordinator+ path, materialization, idempotency, returning-guest resolution); `ClaimInvitationsAsync` passes `GuestMemberId`; `EventInviteeService` + `EventInviteesController` (list/create/revoke with cascade); security events (reuse `InvitationCreated`/`InvitationAccepted`; ids only); OpenAPI regen.
Tests: create→claim→scope end-to-end; second-event-while-pending; revoke cascade; non-escalation; `HouseholdId` rejection.

**Phase 4 — Guest frontend experience**
Middleware role gate; nav trimming; guest dashboard landing + empty state; event-page guest variant; `useEventParticipants`; demo-repo invitee parity + seed data; i18n en/es.
Verification: drive the app as a Guest-role user (live) and in demo mode; deep-link every blocked route.

**Phase 5 — Management UX + polish**
Event "Guests" section (invite modal, invitee list with invitation status + RSVP summary, revoke confirm); directory + household-picker filtering of the Guests household; `/app/profile` own-profile page; report `IsGuest` annotation.

## Open Questions

- Demo-site "view as guest" toggle — worth adding, or is seeded guest data in members' views enough?
- Should `EventInvitee` later drive invite-only events for regular members? The schema already allows it; deliberately out of scope here.

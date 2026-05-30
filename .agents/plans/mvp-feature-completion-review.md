# MVP Feature Completion — Code Review Findings

Branch: `claude/mvp-feature-completion-n2oqj`  
Base: `ad63aed6f5d2510047e2d242864e078e5f8736a1`  
Reviewed: 2026-05-30

## Scope

Four workstreams across ~3000 lines, 52 files:
- **A** — Frontend CRUD pages (households, members, properties, events, meal/task templates)
- **B** — Optional matching TaskTemplate creation from a MealTemplate
- **C** — Event-level meal/attendance report (backend aggregation + frontend page)
- **D** — User invitation with JIT auto-claim on bootstrap

---

## Workstream Completion

| Workstream | Code | Tests |
|---|---|---|
| A — Frontend CRUD pages | ✅ Complete | n/a (UI) |
| B — Meal→Task generation | ✅ Complete | ✅ Three unit tests in `MealTemplateServiceTests.cs` |
| C — Event reports | ✅ Complete | ❌ None — plan required integration + aggregation test |
| D — Invitations | ✅ Complete | ❌ None — plan required invite-create, bootstrap-claim, non-escalation, tenant-isolation tests |

---

## Findings

### 🔴 High — Implement before merge

#### 1. Email claim trust / `email_verified` not checked
**File:** `src/Gatherstead.Api/Services/Provisioning/UserProvisioningService.cs:138-145`

`ResolveEmail` falls back to `preferred_username`, which in Entra/OIDC is **not** a verified email and can be mutable. Auto-claiming invitations from an unverified/attacker-controlled claim lets an authenticated user gain a tenant membership intended for another address.

**Fix:** Require `email_verified == true` before using the `email` claim for invitation matching. Drop the `preferred_username`/`emails` fallbacks from the claim path (or at minimum document the IdP guarantee).

```csharp
// Only trust email claim when explicitly verified by the IdP.
var emailVerified = principal.FindFirst("email_verified")?.Value;
if (!string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase))
    return null;
```

---

#### 2. Template modals unconditionally close on failed save
**Files:**
- `src/Gatherstead.Web/app/components/GsMealTemplateModal.vue:79`
- `src/Gatherstead.Web/app/components/GsTaskTemplateModal.vue` (same pattern)

`await createTemplate(...) / updateTemplate(...)` is called and then `open.value = false` unconditionally. The action functions (`useMealPlans.ts:18-48`) catch errors internally, show a toast, and return `void`, so the modal dismisses on validation failure and the user loses their form input.

**Fix:** Have `createTemplate`/`updateTemplate` return `boolean` success (matching the established pattern in `useTenantUsers.invite()`), then gate the modal close on it:

```typescript
// useMealPlans.ts — createTemplate and updateTemplate
async function createTemplate(...): Promise<boolean> {
  try {
    await repo.createTemplate(...)
    await refresh()
    return true
  } catch (e) {
    toast.add(...)
    return false
  }
}

// GsMealTemplateModal.vue submit()
const ok = await (isEdit.value ? updateTemplate(...) : createTemplate(...))
if (ok) open.value = false
```

---

### 🟡 Medium — Address before or shortly after merge

#### 3. Missing backend tests for Workstreams C and D
The plan explicitly required:
- `InvitationService`: invite-create (new vs existing user), non-escalation, duplicate-pending idempotency
- `UserProvisioningService.BootstrapAsync`: claim flow, user upsert, email refresh
- `EventReportService`: per-day/meal count aggregation, dietary tally, empty-event case, Coordinator-only authorization

These are the highest-risk areas (auth, privilege grants, cross-tenant data) and currently have zero backend tests.

---

#### 4. Shared membership-grant logic duplicated
`InvitationService.GrantMembershipAsync` (line 161) and `UserProvisioningService.ClaimInvitationsAsync` (lines 99-127) both implement the same "upsert TenantUser + optional HouseholdUser if absent" pattern with subtle differences (`IgnoreQueryFilters` in one, not the other).

**Fix:** Extract a shared `MembershipGrantHelper` or a method on a shared service.

---

#### 5. No DB uniqueness guard on pending invitations
`InvitationService.CreateAsync` uses a read-then-write idempotency check (lines 71-78) with no unique index, so two concurrent requests for the same `(TenantId, Email)` can both create Pending rows.

**Fix:** Add a filtered unique index: `(TenantId, Email) WHERE Status = Pending`.

---

#### 6. `create-member.vue` silently no-ops on empty name with no feedback
**File:** `src/Gatherstead.Web/app/pages/app/directory/[householdId]/create-member.vue:38`

`if (!form.name.trim()) return` with no error message and no `:error` binding, inconsistent with every sibling form.

**Fix:** Add a `nameError` ref + `validation.required` i18n message + `:error` binding on the `UFormField`.

---

### 🟢 Low

#### 7. Reports pages lack a UI-level role gate
**Files:** `app/pages/app/reports/index.vue`, `app/pages/app/reports/events/[eventId].vue`

The backend correctly enforces Coordinator+, so a Member who deep-links gets a 403 and an empty state — no data leaks. But the page shows no "you don't have access" message and the entry button (`events/[eventId]/index.vue`) is already gated.

**Fix:** Wrap report content in `<GsRoleGate min-role="Coordinator">` with a fallback message.

---

#### 8. Report access floor (Coordinator) is stricter than plan (Member+)
`EventReportService` gates the full report behind `AuthorizeEventManageAsync` (Coordinator+). The plan specified `AuthorizeGlobalSensitiveReadAsync` (Member+) for dietary data gating. This is safe (more restrictive), but may not match product intent if plain Members should see attendance counts.

**Decision needed:** Is Coordinator+ the intentional floor, or should Members see headcounts with dietary details gated separately?

---

#### 9. `RevokeAsync` sets both `Status = Revoked` and `IsDeleted = true`
**File:** `src/Gatherstead.Api/Services/Invitations/InvitationService.cs:153-155`

Because the global soft-delete filter hides `IsDeleted` rows, revoked invitations vanish from `ListAsync` entirely, making the `Revoked` status unobservable. Either don't soft-delete (rely on `Status`) or have `ListAsync` use `IgnoreQueryFilters` to surface revoked items.

---

#### 10. Inline household-edit modal duplicates `GsCreateHouseholdModal`
**File:** `app/pages/app/directory/[householdId]/index.vue:159-180`

Reimplements the same `UModal/UFormField/name-validation` block already in `GsCreateHouseholdModal.vue`.

**Fix:** Extract a shared `GsHouseholdModal.vue` (create + edit in one component), mirroring the meal/task modal approach.

---

#### 11. Date-sub-range sub-form duplicated across template modals
`GsMealTemplateModal.vue` and `GsTaskTemplateModal.vue` are ~90% structurally identical with the `useSubRange` + two date inputs + `errors.dates` block duplicated verbatim.

**Fix:** Extract a shared `GsTemplateDateRangeField.vue` composable/component.

---

### Nit

- **Demo matching-task time-slot mapping** (`DemoMealPlanRepository.ts`) sets `timeSlots = meal.mealTypes` directly, relying on coincidental flag-value alignment (Breakfast/Lunch/Dinner = 0x01/0x02/0x04 = Morning/Midday/Evening). Add a comment or explicit map to prevent silent breakage if either flag set changes.
- **Demo dietary tally is case-sensitive** vs. backend `OrdinalIgnoreCase`. "Vegan"/"vegan" produces two tallies in demo, one in live. Also demo reads only `dietaryTags` (no `DietaryProfile` allergies/restrictions) — acceptable but documented.
- **Demo invitations in a module-level array**, not `DemoStore`/`persistDemoStore`, so they don't survive reload. Fine for demo, flagged for consistency.
- **`useEventReport` exposes `error`** but the report page ignores it, falling through to a generic `notFound` empty state.
- **`events/[eventId]/index.vue:35`** builds `tabs` as a plain array, so labels won't update on locale switch without remount (pre-existing pattern in a touched file).

---

## Confirmed Clean

- No `$fetch` in any new/changed page or component. The pre-existing `edit.vue` (member) hit is out of scope.
- `repositories.client.ts`, `interfaces.ts`, `types.ts` are fully typed with no `any` or unsafe casts in new code.
- en/es i18n are in exact parity; every key referenced by new code exists in both locale files.
- `Invitation` entity correctly picks up the global soft-delete + tenant query filters via `ApplyGlobalFilters` (reflection-based, covers all `IAuditableEntity` types including new ones automatically).
- Demo↔Live report shape and aggregation logic are equivalent; differences are documented demo-only limitations.

---

## Suggested Fix Order

| # | Severity | Item | Est. Effort |
|---|---|---|---|
| 1 | 🔴 | Email `email_verified` hardening in `UserProvisioningService` | S |
| 2 | 🔴 | Template modals return `boolean` success, gate close on result | S |
| 3 | 🟡 | Backend tests: `InvitationService`, `BootstrapAsync`, `EventReportService` | L |
| 4 | 🟡 | Extract shared membership-grant helper | M |
| 5 | 🟡 | `create-member.vue` empty-name validation feedback | S |
| 6 | 🟡 | Filtered unique index on pending invitations | S |
| 7 | 🟢 | `GsRoleGate` wrapping on report pages | S |
| 8 | 🟢 | Confirm Coordinator vs Member+ intent for report access | Decision |
| 9 | 🟢 | `RevokeAsync` soft-delete vs status-only decision | S |
| 10 | 🟢 | `GsHouseholdModal` extraction | M |
| 11 | 🟢 | Template date-range sub-component extraction | M |

---

## Final Step — Update `/docs` When Editing Is Done

After all fixes are applied and the PR is merged, update the following documentation files to reflect the new capabilities:

- **`docs/IMPLEMENTATION_STATUS.md`** — Mark Workstreams A–D as complete; note the invitation/bootstrap flow, `POST /api/me/bootstrap`, and the Reports section; update schema notes for `User.Email` and new `Invitation` entity.
- **`docs/ARCHITECTURE.md`** — Add the `Reports` and `Invitations` service namespaces to the backend service layer overview; document the JIT provisioning + email-claim auto-claim pattern; note that `HttpContextCurrentUserContext.CacheKey` is intentionally public for the bootstrap flow.
- **`docs/DESIGN_PRINCIPLES.md`** — Note the `email_verified` requirement for invitation matching under the security/privacy section.
- **`README.md`** — Update feature list to include user invitations and event reports.

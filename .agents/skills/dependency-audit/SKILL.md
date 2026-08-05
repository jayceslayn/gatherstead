---
name: dependency-audit
description: Run a solution-wide dependency audit or upgrade across the .NET solution, the Nuxt web app, and the Astro docs site. Caches known blockers, registry-query recipes, and lockfile mechanics so each pass starts from evidence instead of re-deriving it.
updated: 2026-08-04
commit: 97b5ac7
---

# Gatherstead — Dependency Audit & Upgrade

Read this before proposing *any* dependency version change. Section 3 in particular
will stop you recommending upgrades that look routine and are not.

Policy (stand-off windows, CVE tiers, emergency runbook) lives in
[docs/SECURITY-DEPS.md](../../../docs/SECURITY-DEPS.md). This skill is the *method* and
the *cached findings*; that doc is the *rules*.

---

## 1. Topology

**.NET** — 7 projects, all `net10.0`.

| Fact | Detail |
|---|---|
| Central Package Management | **Not used.** No `Directory.Packages.props`, no `Directory.Build.props`. Versions are per-csproj; `TargetFramework`/`Nullable`/`RestorePackagesWithLockFile` are duplicated in all 7. |
| SDK pin | **None.** No `global.json`; CI floats `dotnet-version: 10.0.x`. |
| Lock files | 5 projects commit `packages.lock.json` via `RestorePackagesWithLockFile`. CI restores `--locked-mode`. |
| Outside all CI gates | `tools/FlagsCodegen` — **absent from `Gatherstead.sln`**, no lock file. It is load-bearing anyway: `scripts/generate-openapi.sh` runs it to emit `flags.gen.ts`. |
| Local tools | `.config/dotnet-tools.json` — `dotnet-ef`, `swashbuckle.aspnetcore.cli`. |

**JavaScript** — **two independent pnpm installs, not a workspace.**

| | `src/Gatherstead.Web` | `docs-site` |
|---|---|---|
| Stack | Nuxt 4 · Vue 3 · @nuxt/ui | Astro · Starlight |
| Lockfile | own `pnpm-lock.yaml` (v9.0) | own `pnpm-lock.yaml` (v9.0) |
| `pnpm-workspace.yaml` | `allowBuilds` + a large curated `overrides` block | `allowBuilds` + `overrides` |

There is **no root `package.json`** and neither `pnpm-workspace.yaml` declares
`packages:` — the files exist only to carry `allowBuilds`/`overrides`. Never try to
install from the repo root; always `cd` into one of the two directories.

---

## 2. Environment & query recipes

Node and pnpm are **not on `PATH`**. Node is managed by fnm:

```bash
export PATH="/c/Users/AlexM/AppData/Roaming/fnm/node-versions/v24.14.0/installation:$PATH"
```

That directory also holds the `pnpm` shim. `.node-version` is `24`; App Service runs
`NODE|24-lts`.

**Never run `pnpm build` or `pnpm run lint` yourself — hand those to the user.**

### Latest versions

```bash
# npm — .dist-tags.latest for newest, .time[ver] for publish dates (stand-off maths)
curl -s https://registry.npmjs.org/<pkg>          # URL-encode "/" as %2F for scoped
# NuGet — .versions[], filter entries containing "-" to drop prereleases
curl -s https://api.nuget.org/v3-flatcontainer/<lowercase-id>/index.json
# NuGet publish dates + dependency groups
curl -s https://api.nuget.org/v3/registration5-semver1/<lowercase-id>/index.json
```

### The advisory check that actually matters

Check **resolved** versions from the lockfile, not declared ranges. This is the
endpoint `pnpm audit` uses:

```bash
curl -s -X POST https://registry.npmjs.org/-/npm/v1/security/advisories/bulk \
  -H 'content-type: application/json' \
  -d '{"postcss":["8.5.15"],"brace-expansion":["1.1.16","2.1.2","5.0.7"]}'
```

Get the resolved versions with `grep -E "^  <pkg>@" <lockfile>`.

> **Do not** use `GET /advisories?affects=<pkg>` and read the first `vulnerabilities`
> entry. One GHSA routinely lists several *disjoint* affected ranges — one per major
> line — and reading only the first will tell you a vulnerable version is safe. That
> mistake nearly hid the `svgo` and `fast-uri` findings in the 2026-08 pass. The bulk
> endpoint evaluates every range for you.

A green CI run is **not** evidence of no advisories: `dependency-audit.yml` only fires
on push/PR, so advisories published after the last run sit undetected until the next
PR — which then goes red for reasons unrelated to its own diff. Always sweep first.

---

## 3. Known blockers — check before proposing a frontend bump

Verified from registry metadata and the installed tree, 2026-08-04.

### The frontend chokepoint: `@nuxt/ui` → unhead 3

`nuxt` ≥ 4.5.0 depends on `unhead ^3` / `@unhead/vue ^3`. `@nuxt/ui` 4.10.0 (latest)
still depends on `@unhead/vue ^2.1.15`, and calls `hookOnce`
(`@nuxt/ui/dist/runtime/plugins/colors.js`), which unhead 3 removed.

Consequences:
- **`nuxt` is pinned at 4.4.8** — already the newest 4.4.x.
- The `unhead: ">=2.1.13 <3"` override in `src/Gatherstead.Web/pnpm-workspace.yaml`
  must stay bounded below 3.
- **Watch item:** the `@nuxt/ui` release that adopts unhead 3 unblocks this entire
  cluster. Treat it as the trigger for a dedicated frontend-upgrade PR.

### `@nuxt/kit` must be a singleton — 3 modules transitively blocked

| Module | Next release | Requires |
|---|---|---|
| `@nuxt/icon` | 2.4.1 | `@nuxt/kit ^4.5.0` |
| `@nuxt/image` | 2.1.0 | `@nuxt/kit ^4.5.1` |
| `@nuxtjs/i18n` | 10.5.0+ | `@nuxt/kit ^4.5.x` **and** `unhead ^3.2.x` |

There is **no intermediate version** — each module's very next release jumps straight
to kit 4.5.x. A nested second `@nuxt/kit` duplicates the schema/hook registry and
fails subtly rather than loudly, so a green build does not clear it. Always verify:

```bash
pnpm why @nuxt/kit    # exactly one 4.x entry, and it must be 4.4.x
pnpm why unhead       # must show no 3.x
```

A `@nuxt/kit@3.21.8` entry alongside `4.4.8` is **pre-existing and expected** — an
unrelated legacy module pulls the 3.x line. The invariant is one entry on the *4.x*
line, not one entry overall.

These are *minor* bumps, so Dependabot files them in the `npm-minor` group where
policy says merge-once-green. `.github/dependabot.yml` carries `ignore` entries for
all three; remove them only together with the nuxt 4.5 upgrade.

### Other hard holds

| Package | Why |
|---|---|
| **TypeScript 7** | `@nuxt/ui` 4.10.0 peers `typescript ^5.6.3 \|\| ^6.0.0`; `@astrojs/check` 0.9.10 peers `^5.0.0 \|\| ^6.0.0`. **Both** projects hold at 6.0.3. |
| **FullCalendar 7** | Two independent walls. `daygrid`/`list` have **no 7.x published** (max 6.1.21) *and* peer `@fullcalendar/core ~6.1.21` (tilde pins the minor); `@fullcalendar/vue3@7.0.2` depends on `core` **exactly** 7.0.2. Bumping core+vue3 yields two resolved copies of core and breaks the plugin registry at runtime. All four must move together. |
| **`@types/node` 26** | Runtime is Node 24. Typing against Node 26 APIs invites code that compiles and fails at runtime. Track the runtime major; stay on 25.x. |
| **`Microsoft.Data.SqlClient` trio** | `Microsoft.Data.SqlClient`, `.Extensions.Azure`, and `.AlwaysEncrypted.AzureKeyVaultProvider` must share an **identical** version or `SqlAuthenticationProviderManager`'s static initializer throws. Held at 7.0.2 deliberately *above* what EF Core resolves, because the AKV provider 7.x requires MDS 7.x. Never bump one alone — the csproj comments explain it. |

### Override comments that are stale

`src/Gatherstead.Web/pnpm-workspace.yaml`'s `vite` override says it is bounded
`>=7.3.2 <8` partly because "vitest caps at ^7". That clause is **wrong now** — vitest
4 peers `vite ^6 || ^7 || ^8`. The real and still-valid reason is that nuxt 4.4.8 pins
vite 7.3.3 via `@nuxt/vite-builder`.

### Coupled pairs — never bump one without the other

- `pinia` + `@pinia/nuxt` (`@pinia/nuxt` 1.0.1 peers `pinia ^4.0.2`)
- `vitest` + `@nuxt/test-utils` (`@nuxt/test-utils` 4.1.0 peers `vitest ^4.0.2`)
- the `Microsoft.Data.SqlClient` trio (above)
- `dotnet-ef` tool ↔ the EF Core packages
- `Swashbuckle.AspNetCore` package ↔ `swashbuckle.aspnetcore.cli` tool

---

## 4. .NET lockfile mechanics

| Command | Behaviour |
|---|---|
| `dotnet restore Gatherstead.sln` | Regenerates locks when a *requested range* changed. Does **not** re-evaluate wildcard floats while a lock exists. **This is the default you want** — minimal diff. |
| `dotnet restore --force-evaluate` | Re-resolves floats too. **Avoid** unless you intend the wildcards to move. |
| `dotnet restore --locked-mode` | Never writes; fails NU1004 on mismatch. Verification only. |

**The csproj change and the regenerated lock must be in the same commit.** Splitting
them breaks `--locked-mode` in five CI jobs at once: `build-backend`,
`check-openapi-freshness`, `deploy-migrations`, `deploy-api`, `audit-nuget`.

**Local SDK ≠ CI SDK.** Local is 10.0.103 (targeting packs 10.0.3); CI installs
10.0.302 (runtime 10.0.10). .NET 10 framework-package pruning is sensitive to this, so
a local `--locked-mode` pass does **not** prove CI passes. Run a clean-tree
`--locked-mode` baseline before editing anything; if it fails on a clean tree, stop —
the local SDK cannot reproduce the committed lock and the work belongs in CI.

Per-batch loop: edit csproj → `dotnet restore Gatherstead.sln` →
`git diff --stat -- "**/packages.lock.json"` and **read the diff** →
`dotnet restore Gatherstead.sln --locked-mode` → `dotnet build` → `dotnet test`.

A lock diff larger than the packages you touched is a stop signal: pruning diverged
and CI may reject the lock.

Note `Microsoft.Extensions.Caching.Hybrid` appears in **two** locks —
`src/Gatherstead.Api` and `tests/Gatherstead.Api.Tests` (via ProjectReference).

---

## 5. CI topology

| Workflow | Jobs |
|---|---|
| `ci-cd.yml` | `build-backend`, `build-frontend`, `check-openapi-freshness`, `deploy-migrations`, `deploy-api`, `deploy-web`, `deploy-demo` |
| `dependency-audit.yml` | `audit-nuget`, `audit-pnpm`, `audit-pnpm-docs`, `dependency-review` |
| `docs.yml` | Astro → GitHub Pages; path-filtered to `docs-site/**` |

- **pnpm version** comes from each project's `packageManager` field. All
  `pnpm/action-setup@v6` steps omit `version:` and set `package_json_file:` instead.
  Keep it that way — hardcoding `version:` is what let three different pnpm versions
  act on one lockfile before 2026-08.
- **`check-openapi-freshness`** diffs freshly generated OpenAPI against the committed
  `src/Gatherstead.Api/openapi.json`. The regeneration script is
  `scripts/generate-openapi.sh`, and it rewrites **three** artifacts:
  `openapi.json`, then `app/repositories/generated/api.d.ts` (via `pnpm generate:types`),
  then `flags.gen.ts` (via FlagsCodegen). A change that perturbs the OpenAPI output
  therefore propagates into committed *frontend* types, and all of it must land in one
  commit. The script needs pnpm, so hand it to the user.
- `pnpm build` on the web app is the load-bearing frontend gate: `nuxt.config.ts` sets
  `typescript: { typeCheck: 'build' }`, so `vue-tsc` runs there and a bad
  `@nuxt/ui` / `vue-router` / `@types/node` combination surfaces as a type error.

---

## 6. Stand-off enforcement

Windows are in [docs/SECURITY-DEPS.md](../../../docs/SECURITY-DEPS.md): patch 3d,
minor 7d, major 30d + changelog review.

**Caret ranges cannot enforce this.** A regenerated lockfile always resolves to the
newest matching version, so `^7.1.4` silently becomes 7.1.6. The mechanical fix is
pnpm's own `minimumReleaseAge` (minutes; supported in both 10.33.0 and 11.15.1 —
verified in `dist/pnpm.cjs`), set in each `pnpm-workspace.yaml`. Emergency escape
hatches are `--config.minimumReleaseAge=0` and `minimumReleaseAgeExclude`.

> **Sequencing trap.** pnpm validates `minimumReleaseAge` against the *committed
> lockfile* during `pnpm install --frozen-lockfile`, not only during fresh resolution.
> Enabling it on top of a lockfile that already contains a recently-published
> transitive entry makes **every CI install fail**. Enable it either after the lockfile
> has aged past the window, or together with a full fresh re-resolution — and always
> verify with `--frozen-lockfile` in *both* projects before committing. This is why the
> 2026-08 pass left it as a dated `TODO(deps)` rather than shipping it: both lockfiles
> pinned `nanoid@3.3.17`, two days old at the time.

`allowBuilds` **is** supported on pnpm 10.33.0 as well as 11.x — 10.33.0 translates it
onto `onlyBuiltDependencies` internally. Do not claim the older CI pnpm silently
disabled the build-script allowlist; it did not.

---

## 7. Pass procedure

1. **Baseline gate, no edits.** Clean tree + `dotnet restore --locked-mode`, `build`
   (0 errors, 0 warnings), `test`. Regenerate OpenAPI to scratch and confirm the diff
   against the committed file is **empty** before touching tooling. Ask the user for
   the frontend baseline (`pnpm install --frozen-lockfile`, lint, test, build in both
   projects).
2. **Advisory sweep** on resolved versions from both lockfiles (§2). Anything HIGH
   goes in its own `sec/` PR first, per the emergency-patch runbook — ahead of all
   routine work.
3. **Registry sweep** for latest versions of every direct dependency (§2).
4. **Filter through §3** — blockers and coupled pairs — before believing any diff is
   routine. Check peer deps and `@nuxt/kit` ranges, not just version numbers.
5. **Apply stand-off windows** (§6) using real publish dates.
6. **Batch, verify, commit** — one logical change per commit, each independently
   revertable, each with the narrowest command that would fail if it were wrong.

### Stop signals

- Clean-tree `--locked-mode` fails, or the baseline OpenAPI diff is non-empty.
- A .NET lock diff shows more than the packages you touched.
- `pnpm audit --audit-level=high` still reports HIGH after an override edit — the
  override isn't reaching the consumer; re-check the *resolved* version.
- `pnpm install --frozen-lockfile` fails after an override edit → regenerate the lock;
  never hand-patch one.
- `pnpm why @nuxt/kit` shows more than one version, or `pnpm why unhead` shows 3.x.
- A new advisory publishes mid-pass → finish the batch, then re-sweep.

---

## 8. Deferred queue

Carried forward from the 2026-08-04 pass. Dates are when the stand-off window opens.

| Item | Blocked by | Revisit |
|---|---|---|
| `nuxt` 4.5.x | unhead 3 vs `@nuxt/ui` (§3) | when @nuxt/ui adopts unhead 3 |
| `@nuxt/icon`, `@nuxt/image`, `@nuxtjs/i18n` | `@nuxt/kit ^4.5.x` (§3) | with nuxt 4.5 |
| `typescript` 7 | `@nuxt/ui` + `@astrojs/check` peers (§3) | ecosystem |
| `@fullcalendar/*` 7 | tilde peer + exact dep (§3) | when daygrid/list ship 7.x |
| `@types/node` 26 | runtime is Node 24 | with the runtime |
| `pinia` 4 + `@pinia/nuxt` 1 | 30-day major window | 2026-08-15 |
| `vitest` 4 + `@nuxt/test-utils` 4 | 30-day window; `@nuxt/test-utils` is the binding constraint | 2026-08-26 |
| pnpm 11.20.0 | published 2026-08-03 | 2026-08-10 |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | only prerelease in the tree; no stable exists at all | no functional need |

### Known-good structural work, not yet done

- **Enable `minimumReleaseAge: 4320`** in both `pnpm-workspace.yaml` files — dated
  `TODO(deps)` in each, safe from 2026-08-06. See the sequencing trap in §6.
- Pin the SDK with a `global.json` and switch the five `setup-dotnet` steps to
  `global-json-file` — the root-cause fix for the local-vs-CI divergence in §4.
- Adopt Central Package Management (`Directory.Packages.props`) and a
  `Directory.Build.props` to de-duplicate the 7 csprojs.
- Add `tools/FlagsCodegen` to `Gatherstead.sln` with `RestorePackagesWithLockFile`.
- Bound the test-project wildcard floats (`18.*`, `3.*`, `4.*`, `10.*`). Inert in CI
  while a lock exists; they bite at regeneration time, and bounding them also makes
  the `xunit 4.0.0-pre` prereleases unreachable by accident.

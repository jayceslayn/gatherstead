---
updated: 2026-08-05
commit: 97b5ac7
---

# Dependency Security & Update Policy

This document describes how we keep Gatherstead's dependencies current while
limiting exposure to malicious supply-chain attacks.

## Goals

1. Apply security patches promptly.
2. Let the community flag malicious publishes *before* we adopt them.
3. Keep dev-time friction low — most upgrades should flow through Dependabot
   with minimal human effort.

## Stand-off tiers

| Update type | Stand-off window | Rationale |
|---|---|---|
| Direct dep, patch (`x.y.Z`) | **3 days** | Catches the typosquat / postinstall-script window. |
| Direct dep, minor (`x.Y.z`) | **7 days** | Lets community regressions surface. |
| Direct dep, major (`X.y.z`) | **30 days** + changelog review | Breaking changes warrant a manual gate regardless. |
| Transitive (lockfile-only) | **3 days** | Most supply-chain attacks land here. |
| Dev-only deps | Same as above | Dev deps run in CI and have blast radius (secret exfil). |

Dependabot is scheduled weekly on Monday mornings (see
[.github/dependabot.yml](../.github/dependabot.yml)). That gives Friday and
weekend publishes roughly 72 hours in the wild before we see a PR — an
imperfect but low-effort proxy for the stand-off above.

### Mechanical enforcement

Caret ranges **cannot** enforce the table above: a regenerated lockfile always
resolves to the newest matching version, so `^7.1.4` silently becomes `7.1.6`.

pnpm can enforce it natively at resolution time via `minimumReleaseAge` (minutes), which
is supported on pnpm 10.33.0 and 11.x alike — so Renovate is **not** required for it, as
an earlier version of this document claimed. Escape hatches:

- `pnpm install --config.minimumReleaseAge=0` for a one-off bypass (emergency runbook).
- `minimumReleaseAgeExclude` in `pnpm-workspace.yaml` to exempt a specific package.

**Not yet enabled.** There is a sequencing trap worth knowing: pnpm validates the policy
against the *committed lockfile* during `pnpm install --frozen-lockfile`, not just during
fresh resolution. So enabling it on top of a lockfile that already contains a
recently-published transitive entry makes **every CI install fail**. Both lockfiles
currently pin `nanoid@3.3.17` (published 2026-08-03), which a 3-day cutoff rejects.

Both `pnpm-workspace.yaml` files carry a dated `TODO(deps)` with the enablement steps;
it is safe from **2026-08-06**. Verify with `pnpm install --frozen-lockfile` in *both*
projects before committing. More generally: introduce this setting either when the
lockfile has aged past the window, or together with a full fresh re-resolution.

## Security updates (CVE published, not actively exploited)

- CVSS < 7.0 → apply after **48 hours** community confirmation.
- CVSS 7.0–8.9 → apply after **24 hours** community confirmation.
- CVSS ≥ 9.0 with public PoC → apply **within 24 hours** (see below).

Dependabot opens security-update PRs immediately regardless of the weekly
schedule. Triage them against this table rather than auto-merging.

## Exception — skip the stand-off

Patch immediately when **both** conditions hold:

1. **CVE is formally published** (NVD, GHSA, or vendor advisory — not just a
   tweet or blog post).
2. **Evidence of active exploitation** from one of:
   - Listed on [CISA KEV](https://www.cisa.gov/known-exploited-vulnerabilities-catalog).
   - Vendor bulletin says "actively exploited in the wild."
   - Multiple credible incident-response reports (Mandiant, CrowdStrike, etc.).
   - Exploit weaponised in ransomware or botnet tooling.

**AND** our deployment path actually reaches the vulnerable code. For
Gatherstead that means JWT middleware, EF Core, the ASP.NET Core request
pipeline, or Nuxt SSR are always in scope; a vuln in a dev-only linter
usually is not.

If only criterion 1 holds (CVE published, no exploitation signal) but CVSS
≥ 9.0 with a public PoC, escalate from the 24-hour window to **within hours**
— but still don't skip Tier 1 routine patches.

## Emergency-patch runbook

1. Create a branch named `sec/<cve-id>` off `main`.
2. Bump the affected package(s) to the fixed version in `.csproj` or
   `package.json`. Regenerate lockfiles (`dotnet restore` / `pnpm install`).
3. Open a PR titled `SECURITY: <CVE-ID> <package>` with the advisory URL in
   the body. Skip the usual "grouped Dependabot queue" flow.
4. Confirm `audit-nuget`, `audit-pnpm`, and `dependency-review` pass in CI.
5. Merge to `main` after one reviewer, not two. Deploy to production via
   the standard Bicep pipeline (see [DEPLOYMENT.md](DEPLOYMENT.md)).
6. After the fire is out, file a retro note here listing the CVE, the time
   from advisory to merged fix, and any follow-up hardening.

## What the stand-off *doesn't* cover

- **Dormant maintainer backdoors** (xz-utils style). Defences: SBOM,
  provenance attestations, occasional audit of single-maintainer deps.
- **Tag repoint / force-push over a published version.** Lockfile integrity
  hashes (`pnpm-lock.yaml`, NuGet `packages.lock.json`) protect against this
  — keep them committed and ensure CI uses `--frozen-lockfile` and
  `--locked-mode`.
- **Postinstall script exfiltration.** Mitigated by pnpm's `allowBuilds` allowlist in
  [src/Gatherstead.Web/pnpm-workspace.yaml](../src/Gatherstead.Web/pnpm-workspace.yaml)
  and [docs-site/pnpm-workspace.yaml](../docs-site/pnpm-workspace.yaml) — **not** in
  `package.json`, and the key is `allowBuilds`, not the older `onlyBuiltDependencies`
  (pnpm 10.33.0+ accepts both and translates the new name onto the old). Keep both
  lists minimal.

## Known intentional version pins

- `serialize-javascript` is controlled in **two** places that must stay in step:
  `^7.0.7` in [package.json](../src/Gatherstead.Web/package.json) plus a bounded
  override `>=7.0.7 <8` in
  [pnpm-workspace.yaml](../src/Gatherstead.Web/pnpm-workspace.yaml). The override
  shadows the direct dependency, so raising one without the other is a silent no-op.
  It stays bounded below 8 because the library has a history of prototype-pollution
  issues and we want every major to be explicit. Dependabot ignores its minors/majors.
- The web `overrides:` block carries a set of **bounded transitive advisory floors**
  (postcss, `brace-expansion` split across three major lines, ws, tar, valibot, svgo,
  sharp, js-yaml, happy-dom, …). Each bound exists for a documented reason recorded in
  the file's comments — read them before widening or removing one. `docs-site` has its
  own smaller block; it deliberately does **not** mirror the web one, because astro
  7.1.x requires vite 8 while the web app is bounded to vite 7.
- Test-project NuGet packages (`xunit.v3 3.*`, `Moq 4.*`,
  `Microsoft.NET.Test.Sdk 18.*`, `coverlet.collector 10.*`) use wildcard floats.
  Consider tightening to bounded ranges (e.g. `[18.8.0,19.0.0)`) — this would also
  make the `xunit.v3 4.0.0-pre` prereleases unreachable by accident. The floats are
  inert in CI while a `packages.lock.json` exists; they only bite at regeneration time.

## Retro — 2026-08 nuxt advisory wave

On 2026-08-05 a wave of advisories landed against the Nuxt stack, five days after this
repo's dependency audit had gone green. `pnpm audit` turned the open PR red without any
change to its own diff:

| Package | Severity | Advisory |
|---|---|---|
| `@nuxt/devtools` | **CRITICAL 9.6** | GHSA-279x-mwfv-vcqv — unauthenticated DevTools RPC allows arbitrary commands |
| `nuxt` | **HIGH 8.2** | GHSA-hxvh-4h3w-prp9 — route rules silently dropped for mixed-case paths, bypassing rules |
| `nuxt` | **HIGH 8.1** | GHSA-9473-5f9j-94wq — server-side RCE via runtime template |
| `nuxt` | **HIGH 7.5** | GHSA-wm8w-6qjm-cv43 — runtime payload cache discloses another user's SSR data |
| `nuxt` | **HIGH 7.5** | GHSA-9pgf-384g-p7mv — unauthenticated CPU exhaustion |
| `nuxt` | **HIGH 7.5** | GHSA-hxcr-hm88-mpq6 — unauthenticated out-of-memory crash |
| `js-yaml` | **HIGH** | GHSA-8cp3-6hjf-hh8h — quadratic CPU in `!!omap` (4.3.0 still vulnerable) |

Every `nuxt` fix required ≥ 4.5.1, which this repo had previously recorded as blocked.
GHSA-wm8w-6qjm-cv43 is the one that made the decision straightforward: cross-request SSR
payload disclosure is a tenant-isolation failure, which
[DESIGN_PRINCIPLES.md](DESIGN_PRINCIPLES.md) treats as non-negotiable.

Two lessons, both now in the audit skill:

1. **Re-sweep advisories immediately before merge, not only at PR-open.** `pnpm audit`
   gates on the current database; a dependency PR that sits for days is stale by
   definition.
2. **A declared dependency range is not proof of incompatibility.** The nuxt block
   rested on `@nuxt/ui` needing unhead 2. Its *declared* range still said `^2.1.15`, but
   the code had stopped calling the removed API an entire release earlier. Reading the
   shipped code, not the manifest, unblocked the whole upgrade.

### Accepted exception

`@nuxt/test-utils` 4.1.0 was adopted at 13 days against the 30-day major window. It is a
forced transitive consequence of the fix chain (`nuxt 4.5.x → vite 8 → vitest 4 →
@nuxt/test-utils 4`; no nuxt 4.5.x runs on vite 7), and it is dev-only. The security
track above supersedes the routine stand-off.

## Deferred upgrades

Blockers and dates are maintained in
[.agents/skills/dependency-audit/SKILL.md](../.agents/skills/dependency-audit/SKILL.md)
— see its "Known blockers" and "Deferred queue" sections. As of 2026-08-05 the
significant one is a **frontend chokepoint**: `nuxt` cannot leave 4.4.8 until
`@nuxt/ui` adopts unhead 3, which also transitively holds back `@nuxt/icon`,
`@nuxt/image`, and `@nuxtjs/i18n`. Those three are ignored for minor updates in
`dependabot.yml` because they would otherwise land in the merge-once-green
`npm-minor` group while nesting a second `@nuxt/kit`.

Consult that skill before approving any frontend dependency PR.

## Ownership

- **Dependabot PR triage**: whoever is on-call for the week. Merge after CI
  passes unless the changelog raises a concern.
- **Major-version bumps**: require a follow-up PR from a maintainer who
  read the migration guide. Do not auto-merge.
- **Security exceptions**: anyone can cut an emergency PR; document the
  decision in the PR body.

## Where the automation lives

- [.github/dependabot.yml](../.github/dependabot.yml) — weekly grouped PRs across
  nuget (`/`), npm (`/src/Gatherstead.Web` **and** `/docs-site`), and github-actions.
- [.github/workflows/dependency-audit.yml](../.github/workflows/dependency-audit.yml)
  — `audit-nuget`, `audit-pnpm`, `audit-pnpm-docs`, and `dependency-review` gate every
  PR. (These are **not** in `ci-cd.yml`, which an earlier version of this doc claimed.)
- The pnpm version used by CI comes from each project's `packageManager` field; every
  `pnpm/action-setup` step sets `package_json_file` and omits `version`. Keep it that
  way — hardcoded versions previously let three different pnpm releases act on one
  lockfile.
- `.NET` tool versions are pinned in
  [.config/dotnet-tools.json](../.config/dotnet-tools.json); the one globally-installed
  CI tool (`dotnet-reportgenerator-globaltool`) is pinned inline in `ci-cd.yml`.

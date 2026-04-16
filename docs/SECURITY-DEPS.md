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

If per-package stand-off windows become important, switch to Renovate which
supports `minimumReleaseAge` natively.

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
- **Postinstall script exfiltration.** Mitigated by pnpm's
  `onlyBuiltDependencies` allowlist in [package.json](../src/Gatherstead.Web/package.json).
  Keep that list minimal.

## Known intentional version pins

- `serialize-javascript: 7.0.4` in [package.json](../src/Gatherstead.Web/package.json)
  is pinned exact (no caret) because the library has a history of
  prototype-pollution style issues and we want every bump to be explicit.
- Test-project NuGet packages (`xunit.v3 3.*`, `Moq 4.*`,
  `Microsoft.NET.Test.Sdk 17.*`) use wildcard floats. Consider tightening
  to `3.x.*` / `4.x.*` / `17.x.*` to bound the range while still receiving
  patches.

## Ownership

- **Dependabot PR triage**: whoever is on-call for the week. Merge after CI
  passes unless the changelog raises a concern.
- **Major-version bumps**: require a follow-up PR from a maintainer who
  read the migration guide. Do not auto-merge.
- **Security exceptions**: anyone can cut an emergency PR; document the
  decision in the PR body.

## Where the automation lives

- [.github/dependabot.yml](../.github/dependabot.yml) — weekly grouped PRs
  across nuget, npm, and github-actions.
- [.github/workflows/build-and-test.yml](../.github/workflows/build-and-test.yml)
  — `audit-nuget`, `audit-pnpm`, and `dependency-review` jobs gate every PR.

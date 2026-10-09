---
updated: 2026-10-08
---

# Dependency Security & Update Policy

This policy applies to the .NET solution, the Nuxt web app, and the Astro docs site.

## Update stand-off

| Update type | Minimum age before adoption |
|---|---:|
| Direct dependency patch | 3 days |
| Direct dependency minor | 7 days |
| Direct dependency major | 30 days, with changelog review |
| Transitive lockfile update | 3 days |
| Development dependency | Same window as other dependencies |

The windows reduce exposure to malicious releases and early regressions. A security
response may use the exception below.

### Enforcing the windows

pnpm supports minimumReleaseAge in minutes. It is not enabled in either JavaScript
project. Before enabling it, confirm every resolved lockfile entry satisfies the
window, then verify frozen installs in both projects. pnpm applies the setting to
committed lockfiles as well as new resolutions.

For an approved emergency update, minimumReleaseAgeExclude can exempt a specific
package. Use a one-time zero-age install only when the security exception below
applies, and record the reason in the change review.

## Security response

| Severity | Response |
|---|---|
| CVSS below 7.0 | Apply after 48 hours of community confirmation |
| CVSS 7.0 to 8.9 | Apply after 24 hours of community confirmation |
| CVSS 9.0 or higher with a public proof of concept | Apply within 24 hours |

Skip the stand-off when a vulnerability is formally published and there is evidence
of active exploitation, such as a CISA KEV listing, a vendor statement, or credible
incident-response reporting. Confirm that the affected code is reachable in
Gatherstead. Production request handling, authentication, EF Core, Nuxt SSR, and
dependencies executed in CI are in scope.

If a public proof of concept exists for a critical issue without evidence of active
exploitation, escalate review immediately. Do not treat an advisory's severity alone
as proof of exploitation.

## Emergency patch procedure

1. Create a branch named sec/<cve-id> from main.
2. Update the affected manifest and lockfile.
3. Run the relevant vulnerability audit and build/test gates.
4. Open a security PR with the advisory link and the reason for any stand-off
   exception.
5. Merge after one reviewer when the required checks pass, then deploy through the
   standard pipeline.
6. Update active policy or compatibility notes if the fix changes future dependency
   work. Keep incident timelines and completed-audit narratives out of this policy.

## Controls

- Commit pnpm lockfiles and NuGet packages.lock.json files. CI installs in frozen or
  locked mode.
- Keep pnpm allowBuilds lists minimal in
  [the web workspace](../src/Gatherstead.Web/pnpm-workspace.yaml) and
  [the docs workspace](../docs-site/pnpm-workspace.yaml).
- Each JavaScript app has its own pnpm install and lockfile. Keep overrides specific
  to that app's dependency graph.
- Review resolved versions with npm and NuGet advisory data; declared version ranges
  alone do not show whether the lockfile is vulnerable.

## Current dependency constraints

- The web app declares serialize-javascript directly and also overrides its
  transitive resolution. Keep those ranges synchronized and bounded below the next
  major version.
- Keep Vue and its internal runtime/SSR renderer packages on the same exact release.
  Do not independently override `@vue/server-renderer`; Vue declares matching
  internal versions, and a separate range can resolve an incompatible renderer.
- The current simple-git 4 package exposes the named simpleGit export. Nuxt DevTools
  3.4.1 still imports the removed default export, so
  [a tracked pnpm patch](../src/Gatherstead.Web/patches/@nuxt__devtools@3.4.1.patch)
  updates that import. Remove it only after confirming the candidate upstream
  DevTools package uses the named export; remove its workspace registration and
  patch file, regenerate the lockfile, and pass a frozen install, `pnpm build`, and
  `pnpm run lint` from the web project.
- Microsoft.Data.SqlClient, its Azure extensions, and the Always Encrypted provider
  must use the same version. The Azure provider requires the matching SQL client
  major.
- Keep dotnet-ef aligned with the EF Core packages. Keep the Swashbuckle CLI aligned
  with the Swashbuckle ASP.NET Core package.
- Keep each workspace's advisory overrides bounded to compatible major versions;
  their comments describe the affected package and required floor.

## Open web advisories

As of 2026-10-08, the current web lockfile resolves braces 3.0.3 and node-forge 1.4.0.
Their GitHub advisories list no patched versions, and npm still lists those same versions
as latest. Recheck the advisories, registry, and audit before relying on this status. Do
not force an unpublished version into the lockfile. ([braces advisory](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm),
[node-forge advisory](https://github.com/advisories/GHSA-86w9-cpqp-85rv),
[braces on npm](https://www.npmjs.com/package/braces),
[node-forge on npm](https://www.npmjs.com/package/node-forge))

The web workspace's pnpm audit and the PR dependency-review check temporarily ignore
only these two GHSA IDs so other dependency fixes can pass CI. Remove each exception
as soon as its patched release is published and resolved; the exceptions do not change
the installed versions or indicate that these advisories are fixed.

- [braces GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm)
- [node-forge GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv)

## Automation and ownership

- [.github/dependabot.yml](../.github/dependabot.yml) manages weekly updates for
  NuGet, the web app, the docs site, and GitHub Actions.
- [.github/workflows/dependency-audit.yml](../.github/workflows/dependency-audit.yml)
  runs NuGet, web pnpm, docs pnpm, and dependency-review audits.
- CI reads each project's pnpm version from its packageManager field. Keep that
  field and the CI setup in sync.
- Pin local .NET tools in [.config/dotnet-tools.json](../.config/dotnet-tools.json).
- Maintainers review major-version migrations. Security exceptions belong in the
  security PR that uses them.

---
name: dependency-audit
description: Audit and update dependencies across Gatherstead .NET, Nuxt, and Astro projects using current registry data, compatible lockfile changes, and repository verification gates.
updated: 2026-10-08
---

# Gatherstead Dependency Audit

Use this guide with [docs/SECURITY-DEPS.md](../../../docs/SECURITY-DEPS.md). That document defines the update and security policy; this guide defines the repository layout and audit workflow.

## 1. Repository layout

- The .NET solution contains the API, Data, Data.Setup, API tests, and Data tests. Each project commits a packages.lock.json file. Package versions are declared in project files; Central Package Management is not configured.
- Local .NET tools are pinned in .config/dotnet-tools.json. Keep dotnet-ef aligned with EF Core and the Swashbuckle CLI aligned with the Swashbuckle package.
- tools/FlagsCodegen is outside Gatherstead.sln. Check it separately when a dependency change affects its inputs or outputs.
- The Nuxt app in src/Gatherstead.Web and the Astro site in docs-site use independent pnpm installs and lockfiles. There is no root package.json; run pnpm commands from the relevant app directory.
- CI obtains each pnpm version from that app's packageManager field. Keep the field and its pnpm/action-setup configuration consistent.

## 2. Environment and audit commands

On Windows PowerShell, use pnpm.cmd. If Node or pnpm is not on PATH, load the repository's Node 24 version with fnm; avoid hardcoding an individual user's profile path.

Run advisory audits against resolved lockfile versions:

- From src/Gatherstead.Web and docs-site, run pnpm audit --json.
- From the repository root, run dotnet list Gatherstead.sln package --vulnerable --include-transitive.
- Use pnpm outdated and dotnet list Gatherstead.sln package --outdated to identify direct updates, then check package registry metadata and publish times before applying stand-off windows.

Check full advisory ranges. An advisory may list disjoint affected ranges across major versions; do not infer safety from only the first range or from a declared version range. Review the resolved versions and the consumer path in each lockfile.

For registry checks, npm view <package> versions time --json provides versions and publish times. NuGet's flat-container index lists versions; registration metadata provides publish dates and dependency groups.

## 3. Compatibility checks

Resolve peer and runtime constraints before editing manifests:

- Nuxt upgrades can change the required Vite and test-tool majors. Check Nuxt, @nuxt/vite-builder, Vite, Vitest, and @nuxt/test-utils together against their current peer ranges.
- Check that Nuxt's active @nuxt/kit line is resolved consistently, and that unhead does not resolve incompatible duplicate majors. Use pnpm why before and after Nuxt upgrades.
- Treat FullCalendar core, Vue integration, and plugins as a peer-coupled set. Check every package's current peer and dependency ranges before changing a major.
- Keep Vue and its internal runtime/SSR renderer packages on the same exact release. Do not independently override `@vue/server-renderer`; Vue declares matching internal versions and a separate range can resolve a mismatched renderer.
- Keep @types/node aligned with the Node runtime used by local development and deployment.
- Microsoft.Data.SqlClient, Microsoft.Data.SqlClient.Extensions.Azure, and Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider must use the same version. Keep that trio on a mutually supported major.
- Keep dotnet-ef on the EF Core tool-compatible release line. Keep the Swashbuckle CLI and ASP.NET Core package compatible.
- The current simple-git 4 package exposes the named simpleGit export. Nuxt DevTools 3.4.1 imports the removed default export, so the web project carries a pnpm patch at src/Gatherstead.Web/patches/@nuxt__devtools@3.4.1.patch. Remove it only after confirming the candidate upstream DevTools package uses the named export; then remove the patch registration and file, regenerate the lockfile, and pass a frozen install, `pnpm build`, and `pnpm run lint` from src/Gatherstead.Web.
- Current unresolved web advisories are listed in docs/SECURITY-DEPS.md. Recheck the live audit and registry before relying on that status; never force an unpublished fixed version.

Read the comments in each pnpm-workspace.yaml before changing an override. The web and docs dependency trees differ, so do not copy override lists between them.

## 4. Lockfile mechanics

| Command | Use |
|---|---|
| dotnet restore Gatherstead.sln | Regenerate locks after changing direct package versions; review each packages.lock.json diff. |
| dotnet restore Gatherstead.sln --force-evaluate | Re-resolve floating test-package versions when that is intended. |
| dotnet restore Gatherstead.sln --locked-mode | Verify the committed project and lockfile versions match. |
| pnpm install --frozen-lockfile | Verify each JavaScript manifest, workspace configuration, patch, and lockfile reproduce together. |

Commit each project-file change with its regenerated lockfile. Keep lockfile changes limited to the package trees being updated. Restore local tools after changing .config/dotnet-tools.json.

If API package changes could affect generated OpenAPI, build the API in Release and generate the document to a temporary path. Compare it with src/Gatherstead.Api/openapi.json. Review generated frontend types and flags separately if the contract changes; the repository generation script updates multiple artifacts.

## 5. Audit workflow

1. Review git status and understand existing changes before touching manifests.
2. Run NuGet and both npm advisory audits. Collect resolved vulnerable packages and dependency paths for the active task; do not append completed audit snapshots to this guide.
3. Check direct dependency updates, peer ranges, runtime requirements, and registry publish times.
4. Apply the stand-off windows in docs/SECURITY-DEPS.md. Treat an explicitly documented security exception as higher priority than routine update timing.
5. Update direct manifests and the narrowest corresponding lockfiles. Regenerate locks with the owning package manager.
6. Verify frozen pnpm installs in both JavaScript projects, NuGet locked restore, .NET build and tests, and the repository's frontend build/lint gates. For docs-site changes, run its build and check scripts.
7. Re-run all advisory audits and review the final diff for unrelated lockfile movement.

## 6. Stop signals

- Locked NuGet restore or frozen pnpm install fails after the lockfiles are regenerated.
- A lockfile diff includes unrelated package trees or unexpectedly broad version movement.
- A high-severity advisory remains after an override change; inspect the actual resolved version and all dependency paths.
- A proposed fix depends on a version the registry does not publish.
- A Nuxt package change creates incompatible @nuxt/kit, unhead, Vite, or test-tool resolutions.
- Generated OpenAPI changes unexpectedly. Review the API contract and generated consumers before accepting the change.

## 7. CI references

- .github/workflows/dependency-audit.yml runs NuGet, web pnpm, docs pnpm, and dependency-review checks.
- .github/workflows/ci-cd.yml contains backend, frontend, OpenAPI freshness, migration, API, web, and demo jobs.
- scripts/generate-openapi.sh updates the committed OpenAPI document and generated frontend artifacts. Review all outputs when intentionally changing the contract.
- The web Nuxt build performs TypeScript checking through nuxt.config.ts. Run it alongside lint after web dependency changes.

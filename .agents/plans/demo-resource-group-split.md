# Plan: Split demo resources into their own Resource Group (`rg-gat-demo-wus2`)

## Context

Today the "demo" site is **two resources** — the Static Web App `stapp-gat-demo-prod-wus2`
and a separate App Insights `appi-gat-demo-prod-wus2` — that live **inside the production
resource group** `rg-gat-prod-wus2` and carry `prod` in their names. Conceptually `demo` *is*
its own environment, so its resources should live in their own RG and drop the misleading
`prod` token (the literal `demo` segment becomes the CAF environment token).

This change relocates the two demo resources into a new `rg-gat-demo-wus2` resource group and
renames them so `demo` is the environment, not a discriminator inside prod. Decisions confirmed
with the user:

- **Topology:** keep the single subscription-scoped `az deployment sub create` on
  `main.bicep`; create a **second RG** gated on `deployDemo` and scope the demo module to it.
- **Demo telemetry:** the demo App Insights keeps pointing at the **existing prod Log Analytics
  workspace** `log-gat-prod-wus2` (cost-neutral cross-RG reference); only the App Insights
  *component* moves to the demo RG.
- **Naming:** `rg-gat-demo-wus2`, `stapp-gat-demo-wus2`, `appi-gat-demo-wus2`.

### Migration caveat (operational, not code)
Renaming = new resources. After deploy the old `*-demo-prod-wus2` resources are **orphaned in
the prod RG** (subscription deployments don't run in complete mode) and need manual deletion.
The SWA hostname and the demo App Insights connection string both change, so the repo
vars/secret must be updated (see "Post-deploy wiring").

## Changes

### 1. `infrastructure/modules/staticwebapp.bicep` — becomes the self-contained demo module
This module already owns the demo SWA + the CI Contributor role assignment. Fold the demo App
Insights into it so both demo resources are co-located and scoped to the demo RG.

- Drop the redundant `demo` segment from the SWA name (now `environment='demo'`):
  - `infrastructure/modules/staticwebapp.bicep:20` →
    `name: 'stapp-${workload}-${environment}-${locationAbbreviation}'` → `stapp-gat-demo-wus2`.
- Add a `workspaceId` param (the prod Log Analytics workspace resource id).
- Add the demo App Insights resource (moved verbatim from observability.bicep:65-76), named
  `appi-${workload}-${environment}-${locationAbbreviation}` → `appi-gat-demo-wus2`, with
  `WorkspaceResourceId: workspaceId`. It stays unconditional in the module (the module itself is
  already gated by `if (deployDemo)` at the call site). No role assignment needed — it ingests
  via the public connection string baked into the static build.
- Add output `demoAppInsightsConnectionString string = demoAppInsights.properties.ConnectionString`.

### 2. `infrastructure/modules/observability.bicep` — remove all demo concerns
The demo App Insights moves out, so observability returns to pure-prod scope.

- Remove `param deployDemo` (line 24-25), `var demoAppInsightsName` (line 29), the
  `demoAppInsights` resource (lines 65-76), and the `demoAppInsightsConnectionString` output
  (line 182). Keep the existing `workspaceId` output (line 178) — the demo module consumes it.

### 3. `infrastructure/main.bicep` — second RG + rescope the demo module
- Add param `demoResourceGroupName string` (default e.g. `'gatherstead-demo-rg'`).
- Add a second RG, gated on demo:
  ```bicep
  resource demoRg 'Microsoft.Resources/resourceGroups@2024-03-01' = if (deployDemo) {
    name: demoResourceGroupName
    location: location
  }
  ```
- `observability` module call: drop the `deployDemo: deployDemo` param (line 121).
- `demo` module call (lines 199-209): change `scope: rg` → `scope: demoRg`; pass
  `environment: 'demo'` (literal, overriding the prod `environment`); add
  `workspaceId: observability.outputs.workspaceId`. Keep `ciIdentityPrincipalId`.
- Outputs (lines 228-230): `demoSiteUrl` stays; change `demoAppInsightsConnectionString` to
  `deployDemo ? demo.outputs.demoAppInsightsConnectionString : ''`. Add
  `output demoResourceGroupName string = deployDemo ? demoResourceGroupName : ''` for CI wiring.

### 4. `infrastructure/parameters/prod.bicepparam` + `.example`
- Add `param demoResourceGroupName = 'rg-gat-demo-wus2'`.
- Update the comment at lines 55-57 to say the demo SWA + its `appi-gat-demo-wus2` App Insights
  are provisioned in a **separate** `rg-gat-demo-wus2` (sharing the prod workspace).

### 5. `.github/workflows/ci-cd.yml` — point the demo job at the demo RG
- `deploy-demo` → `Fetch SWA deployment token` step (lines 348-351): change
  `--resource-group "${{ vars.AZURE_RESOURCE_GROUP }}"` → `"${{ vars.DEMO_RESOURCE_GROUP }}"`.
- Update the required-vars comment block (lines 130-135) to add `DEMO_RESOURCE_GROUP` alongside
  `DEMO_SWA_NAME`.
- No change to the build/generate step (it uses the `DEMO_APPINSIGHTS_CONNECTION_STRING` secret,
  whose *value* changes but whose name does not).

### 6. `docs/DEPLOYMENT.md` — demo section
- Update lines ~271-292: demo now lives in its own `rg-gat-demo-wus2`; add the
  `DEMO_RESOURCE_GROUP` repo variable to the required-vars list; note the new resource names.

## Post-deploy wiring (manual, after `az deployment sub create`)
From the new Bicep outputs, update GitHub repo config:
- Repo **variable** `DEMO_SWA_NAME` → `stapp-gat-demo-wus2`.
- Repo **variable** `DEMO_RESOURCE_GROUP` → `rg-gat-demo-wus2` (new).
- Repo **secret** `DEMO_APPINSIGHTS_CONNECTION_STRING` → new `demoAppInsightsConnectionString` output.
- After verifying the new demo site works, **delete the orphaned** `stapp-gat-demo-prod-wus2`
  and `appi-gat-demo-prod-wus2` from `rg-gat-prod-wus2`.

## Verification
1. **Lint/build templates:** `az bicep build --file infrastructure/main.bicep` — 0 errors.
2. **What-if (no apply):**
   `az deployment sub create --location westus2 --template-file infrastructure/main.bicep --parameters infrastructure/parameters/prod.bicepparam --what-if`
   Confirm: a new `rg-gat-demo-wus2` is created; `stapp-gat-demo-wus2` and `appi-gat-demo-wus2`
   appear in it; the old `*-demo-prod-wus2` resources show as **no longer managed** (will be
   orphaned, not deleted); prod resources are unchanged.
3. **Grep guard:** no remaining `demo-${environment}` / `demo-prod` patterns in `infrastructure/`
   except the intended `environment='demo'` literal in the demo module call.
4. The app code (`dotnet build`/`dotnet test`, `pnpm build`/`lint`) is untouched by this change —
   no app build needed, but CI still runs them on the PR.

## Stop signals
- If `az bicep build` reports that a literal `environment: 'demo'` on the demo module call
  conflicts with how other modules consume `environment` (it shouldn't — the param is plain
  string), pause and report.
- If the cross-RG `WorkspaceResourceId` reference from the demo App Insights to the prod
  workspace fails what-if validation, fall back to provisioning a `log-gat-demo-wus2` workspace
  in the demo RG and surface the change.

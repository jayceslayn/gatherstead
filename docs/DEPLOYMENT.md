---
updated: 2026-06-25
commit: 31a127e
---

# Database Encryption and Deployment

This project uses Always Encrypted with Secure Enclaves to protect sensitive data. Infrastructure is managed with **Bicep** (Azure-native IaC) and all resource-to-resource authentication uses **managed identity** — no passwords or connection string secrets.

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) with Bicep extension: `az bicep install`
- Contributor + User Access Administrator roles on the target subscription
- .NET 10 SDK

## CI/CD pipeline

[.github/workflows/ci-cd.yml](../.github/workflows/ci-cd.yml) is a single CI/CD workflow. On every push and PR to `main` it runs the build/test gates; on a push to `main` it then deploys, but **only after** `build-backend` and `build-frontend` succeed. On PRs the deploy jobs are skipped.

**Gate jobs** (run on push + PR):

- **`build-backend`** — `dotnet restore --locked-mode`, build, test.
- **`build-frontend`** - `pnpm build`
- **`audit-nuget`** / **`audit-pnpm`** / **`dependency-review`** (in [dependency-audit.yml](../.github/workflows/dependency-audit.yml)) — fail on vulnerable NuGet/pnpm dependencies. These run independently and do **not** gate the deploy jobs.

**Deploy jobs** (push to `main` only; run in sequence: migrations → api → web/demo in parallel):

- **`deploy-migrations`** — applies an idempotent EF Core script (opens a temporary SQL firewall rule for the runner, then removes it). On a clean database this creates every table; on an existing one it no-ops.
- **`deploy-api`** — zip-deploys the API to App Service; `deploy-web` and `deploy-demo` gate on this job.

> **Always Encrypted setup is not a CI job.** Creating the CMK/CEK and encrypting columns on the
> system-versioned temporal tables requires `CONTROL` on those tables (to toggle `SYSTEM_VERSIONING`)
> plus Key Vault crypto access — a privilege level incompatible with the CI identity's deliberate
> no-read posture. It is run **manually by a SQL admin** via `Gatherstead.Data.Setup`; see
> [Configure Encryption and Temporal Retention](#5-configure-encryption-and-temporal-retention). It is
> idempotent and only needs re-running when PII columns change.
- **`deploy-web`** — zip-deploys the Web app to App Service (runs after `deploy-api`).
- **`deploy-demo`** — generates and uploads the static demo site after `deploy-api` (see [Demo Site](#demo-site)).

Lockfiles (`packages.lock.json` per .NET project, `pnpm-lock.yaml` for the web app) are committed and integrity-checked on every build. Emergency security patches follow the runbook in [SECURITY-DEPS.md](SECURITY-DEPS.md#emergency-patch-runbook) and still deploy via this same pipeline.

### Deploy authentication & required GitHub config

Deploys authenticate to Azure with **GitHub OIDC** federated to the `gat-ci-id-*` user-assigned managed identity provisioned by `ci-identity.bicep` — no client secret. After provisioning infrastructure (and running `ci-grant.sql`, below), configure the repository once:

**Secrets** — `AZURE_CLIENT_ID` (the `ciIdentityClientId` output), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, plus `DEMO_APPINSIGHTS_CONNECTION_STRING`. The demo SWA deployment token is fetched at runtime by the CI identity (`az staticwebapp secrets list`), so no token secret is stored.

**Variables** — `AZURE_RESOURCE_GROUP`, `API_APP_NAME` (`apiAppName` output), `WEB_APP_NAME` (`webAppName` output), `SQL_SERVER_NAME` (`sqlServerName` output), `SQL_DATABASE_NAME` (`sqlDatabaseName` output), and `DEMO_SWA_NAME` (`gat-demo-swa`). The app/server names embed a `uniqueString` hash, so copy them from the Bicep outputs rather than guessing. (`keyVaultCmkId` is no longer a CI variable — it's the CMK Key Vault ID passed to the manual Always Encrypted setup in [step 5](#5-configure-encryption-and-temporal-retention).)

The federated credential trusts `repo:<owner>/<repo>:ref:refs/heads/main` — override `githubRepository` / `githubBranch` in the bicepparam files if your repo slug differs.

## Infrastructure Structure

```
infrastructure/
  main.bicep               # Subscription-scoped root: resource group, wires modules together
  modules/
    identity.bicep         # User-assigned managed identity for the application
    keyvault.bicep         # Key Vault (Premium) + CMK key + RBAC role assignments
    sql.bicep              # SQL Server (Entra ID-only auth) + Database
    appservice.bicep       # App Service Plan + API app (.NET 10) + Web app (Node 24)
    ci-identity.bicep      # GitHub Actions OIDC managed identity + deploy RBAC
  parameters/
    prod.bicepparam        # Environment params — copy from prod.bicepparam.example (gitignored)
  post-deploy.sql          # Grants the app managed identity SQL database access (one-time)
  ci-grant.sql             # Grants the CI identity DDL + write access for migrations (one-time)
```

## App Service Plan SKU: F1 vs B1

Set `appServicePlanSku` in `prod.bicepparam`. Start on F1 to validate the deploy at $0, switch to B1 before go-live.

| | F1 (Free) | B1 (Basic) |
|---|---|---|
| Cost | $0 | ~$13/month/plan |
| CPU | 60 min/day shared | 1 core dedicated |
| RAM | 1 GB shared | 1.75 GB |
| Always On | No | Yes |
| Custom domains | No | Yes |

Both apps (API + Web) share one plan. Scale up `appServicePlanSku` in `prod.bicepparam` as traffic grows (B2, B3, P1v3, etc.).

## Deployment Workflow

### 1. Configure Parameters

Copy `infrastructure/parameters/prod.bicepparam.example` to `prod.bicepparam` (gitignored) and fill in:

- `sqlEntraAdminObjectId` — Object ID of the Entra ID user or group to be SQL admin
  ```bash
  az ad signed-in-user show --query id -o tsv
  ```
- `sqlEntraAdminLogin` — UPN or display name of that user/group
- `deployerObjectId` — Object ID of whoever runs the deployment (gets Key Vault Administrator)
  ```bash
  az ad signed-in-user show --query id -o tsv
  ```

### 2. Provision Infrastructure

```bash
az login
az deployment sub create \
  --location westus2 \
  --template-file infrastructure/main.bicep \
  --parameters infrastructure/parameters/prod.bicepparam
```

This provisions the resource group, managed identity, Key Vault, SQL Server + Database, App Service Plan, API app, and Web app in one step. The managed identity is automatically attached to the API app and all app settings are pre-configured.

Note the deployment outputs — you'll need them in the steps below:

```bash
az deployment sub show \
  --name main \
  --query properties.outputs
```

Key outputs: `managedIdentityName`, `managedIdentityClientId`, `sqlServerFqdn`, `keyVaultUri`, `keyVaultCmkId`, `apiAppUrl`, `webAppUrl`.

To preview changes without applying: replace `create` with `what-if`.

### 3. Grant the Managed Identity SQL Access

Connect to the database as the Entra ID SQL administrator, then run `infrastructure/post-deploy.sql` — replacing `<managed-identity-name>` with the `managedIdentityName` output:

```bash
sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault \
  -i infrastructure/post-deploy.sql
```

Then grant the CI deploy identity the access the pipeline needs to apply migrations — replace `<ci-identity-name>` with the `ciIdentityName` output. This grants `db_ddladmin` (schema changes) **and** `db_datawriter`, because EF migrations carry DML: `HasData` seed data (e.g. the `DietaryTags` reference rows, emitted as `InsertData`/`UpdateData`/`DeleteData`) and any `migrationBuilder.Sql` backfill. It deliberately withholds `db_datareader` — CI can write via reviewed migrations but never reads application data, which (together with column encryption) keeps PII confidential from the CI identity:

```bash
sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault \
  -i infrastructure/ci-grant.sql
```

### 4. Run EF Core Migrations

```bash
dotnet ef database update --project src/Gatherstead.Data
```

The connection string must use Entra ID managed identity auth:

```
Server=tcp:<sql-server-fqdn>,1433;Database=gatherstead;Authentication=Active Directory Managed Identity;User Id=<managedIdentityClientId>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### 5. Configure Encryption and Temporal Retention

**Run this manually as a SQL admin — it is deliberately not part of CI.** Encrypting the
system-versioned temporal tables requires toggling `SYSTEM_VERSIONING`, which needs `CONTROL` on the
current *and* history tables. `CONTROL` implies `SELECT`, so granting it to the no-read CI identity
would defeat the load-bearing PII confidentiality control (see [ci-grant.sql](../infrastructure/ci-grant.sql)).
Run it as an identity in the **`Gatherstead SQL Admins`** group (which has `CONTROL`) that also has
**Key Vault Crypto User** on the vault. It is idempotent, so re-run it whenever PII columns are added or
changed; routine schema migrations (step 4) remain automated in CI.

Authenticate with `az login` as that admin, then run the setup utility with the CMK's **Key Vault key
URL** as the second argument. This must be the key's `https://<vault>.vault.azure.net/keys/...`
identifier — **not** the ARM resource ID. (The Bicep `cmkKeyId` output / `keyVaultCmkId` in
`infrastructure/output.json` is the ARM resource ID and will fail with
`Invalid url specified ... (Parameter 'masterKeyPath')`.) Get the correct value with:

```bash
az keyvault key show --vault-name <vault-name> --name cmk-gatherstead --query key.kid -o tsv
# e.g. https://gat-kv-ut5ftcowqancq.vault.azure.net/keys/cmk-gatherstead
```

The connection string must use `Active Directory Default` (resolved to your CLI session by
`DefaultAzureCredential`) and set `Column Encryption Setting=Enabled`:

```bash
dotnet run --project src/Gatherstead.Data.Setup/Gatherstead.Data.Setup.csproj --configuration Release -- \
  "Server=tcp:<sql-server-fqdn>,1433;Database=gatherstead;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Column Encryption Setting=Enabled;" \
  "<cmkKeyUrl>"
```

This is the single source of truth for column encryption. It creates the Column Master Key (CMK) and
Column Encryption Key (CEK) — the CEK is generated locally and wrapped with the CMK via Key Vault — applies
Always Encrypted to all configured sensitive columns (see `EnsureColumnEncryption` in
`src/Gatherstead.Data.Setup/Program.cs`, which disables/re-enables `SYSTEM_VERSIONING` to encrypt both the
current and history copies of each column), and sets a 1-year retention policy on all temporal history
tables. Every step is idempotent, so it is safe to re-run.

### 6. Deploy the Application

The API and Web App Service apps are already provisioned with the correct configuration (managed identity, connection string, Key Vault URI, CORS, API base URL). Once the GitHub secrets/variables in [Deploy authentication & required GitHub config](#deploy-authentication--required-github-config) are set, pushing to `main` deploys (and applies migrations) automatically. To deploy manually:

**API** (ASP.NET Core 10):
```bash
dotnet publish src/Gatherstead.Api -c Release -o ./publish/api
cd ./publish/api && zip -r ../../api.zip .
az webapp deploy --resource-group <resourceGroupName> --name <apiAppName> --src-path api.zip
```

**Web** (Nuxt 4 SSR, Node 24):
```bash
cd src/Gatherstead.Web
pnpm build
# Keep the .output directory in the archive — the start command is `node .output/server/index.mjs`.
zip -r web.zip .output
az webapp deploy --resource-group <resourceGroupName> --name <webAppName> --src-path web.zip
```

After deployment, the apps are reachable at the `apiAppUrl` and `webAppUrl` outputs.

## Demo Site

A public demo site is deployed alongside production on **Azure Static Web Apps (Free tier)** at `demo.gatherstead.<host>.<ext>`. The demo uses the same codebase built as a static SPA with `NUXT_PUBLIC_DEMO_MODE=true pnpm generate`, using browser localStorage instead of a backend API. No App Service plan, SQL Server, or managed identity is required — zero hosting cost.

A build-time `__DEMO_MODE__` constant (set via `vite.define` when `NUXT_PUBLIC_DEMO_MODE=true`) ensures the live and demo bundles are fully isolated: Rollup eliminates the dead branch entirely so no demo repository code ships in the live build and no live API client code ships in the demo build.

### Infrastructure

`infrastructure/modules/staticwebapp.bicep` provisions the Static Web App resource, gated behind a `deployDemo` parameter in `main.bicep` so it is only created when explicitly enabled. The same `deployDemo` flag also provisions a separate App Insights component (`gat-ai-demo-*`, in `observability.bicep`) so anonymous demo traffic never mixes with prod product metrics.

### CI/CD

The `deploy-demo` job in [.github/workflows/ci-cd.yml](../.github/workflows/ci-cd.yml) builds and deploys the demo on push to `main` (after build + test + `deploy-api` pass), keeping it in sync with the latest code.

### Frontend telemetry

Browser telemetry uses the App Insights JS SDK (see [OBSERVABILITY.md](OBSERVABILITY.md#frontend-telemetry)), delivered via `NUXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING`:

- **Prod** — set automatically as a web-app app setting in `appservice.bicep` (points at the shared `gat-ai-*`, enabling frontend↔backend trace correlation).
- **Demo** — copy the `demoAppInsightsConnectionString` deployment output into the `DEMO_APPINSIGHTS_CONNECTION_STRING` GitHub Actions secret; the `deploy-demo` job in `ci-cd.yml` bakes it into the static build at `pnpm generate` time. It is an ingestion-only key and safe to expose.

See [DEMO_SITE.md](agents/plans/DEMO_SITE.md) for full architecture and implementation details.

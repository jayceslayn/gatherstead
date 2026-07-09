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

> **Always Encrypted setup is not a CI job.** Creating the CMK/CEK and encrypting columns requires
> Key Vault crypto access (to wrap the CEK) that the CI identity does not have, and is driven by the
> `Gatherstead.Data.Setup` tool rather than a raw script. It is run **manually by a SQL admin** via
> `Gatherstead.Data.Setup`; see
> [Configure Encryption and Temporal Retention](#5-configure-encryption-and-temporal-retention). It is
> idempotent and only needs re-running when PII columns change.
- **`deploy-web`** — zip-deploys the Web app to App Service (runs after `deploy-api`).
- **`deploy-demo`** — generates and uploads the static demo site after `deploy-api` (see [Demo Site](#demo-site)).

Lockfiles (`packages.lock.json` per .NET project, `pnpm-lock.yaml` for the web app) are committed and integrity-checked on every build. Emergency security patches follow the runbook in [SECURITY-DEPS.md](SECURITY-DEPS.md#emergency-patch-runbook) and still deploy via this same pipeline.

### Deploy authentication & required GitHub config

Deploys authenticate to Azure with **GitHub OIDC** federated to the `id-gat-ci-*` user-assigned managed identity provisioned by `ci-identity.bicep` — no client secret. After provisioning infrastructure (and running `ci-grant.sql`, below), configure the repository once:

**Secrets** — `AZURE_CLIENT_ID` (the `ciIdentityClientId` output), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, plus `DEMO_APPINSIGHTS_CONNECTION_STRING`. The demo SWA deployment token is fetched at runtime by the CI identity (`az staticwebapp secrets list`), so no token secret is stored.

**Variables** — `AZURE_RESOURCE_GROUP`, `API_APP_NAME` (`apiAppName` output), `WEB_APP_NAME` (`webAppName` output), `SQL_SERVER_NAME` (`sqlServerName` output), `SQL_DATABASE_NAME` (`sqlDatabaseName` output), and `DEMO_SWA_NAME` (`stapp-gat-demo-*`). The app/server names embed a short `uniqueString` hash suffix, so copy them from the Bicep outputs rather than guessing. (`keyVaultCmkId` is no longer a CI variable — it's the CMK Key Vault ID passed to the manual Always Encrypted setup in [step 5](#5-configure-encryption-and-temporal-retention).)

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
  ci-grant.sql             # Grants the CI identity db_owner for migrations (one-time)
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

This grants the API's managed identity **read/write DML only** (`db_datareader` + `db_datawriter`) — the app performs no schema work at runtime (migrations are not applied at startup), so it deliberately gets no `db_ddladmin`. Schema changes are owned by the CI identity (next step). The app identity is therefore lower-privileged than both the `Gatherstead SQL Admins` group and the CI identity. The script is idempotent and safe to re-run.

Then grant the CI deploy identity the access the pipeline needs to apply migrations — replace `<ci-identity-name>` with the `ciIdentityName` output. This grants **`db_owner`**. Migrations need more than DDL + write: every `AuditableEntity` table is system-versioned temporal, and altering an existing temporal table makes EF toggle `SYSTEM_VERSIONING`, which requires `CONTROL` (`db_ddladmin` is insufficient and the migration fails with `Msg 13538`). `db_owner` supplies `CONTROL` and also covers the DML that migrations carry (`HasData` seed data and `migrationBuilder.Sql` backfills). Tradeoff: `db_owner` implies `SELECT`, so the earlier "no-read" posture is dropped; Always-Encrypted PII columns stay ciphertext to CI (it has no Key Vault crypto access) while non-encrypted columns are readable:

```bash
sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault \
  -i infrastructure/ci-grant.sql
```

### 4. Run EF Core Migrations

The design-time factory (`src/Gatherstead.Data/GathersteadDbContextFactory.cs`) hardcodes a dummy
`Server=localhost;…` connection string and ignores design-time args, so a bare
`dotnet ef database update` targets localhost, **not** Azure. You must supply the real connection
explicitly. A manual run from a developer machine authenticates with `Active Directory Default`
(`DefaultAzureCredential` resolves your `az login` session) — **not** `Active Directory Managed Identity`,
which only resolves from inside Azure compute.

Prerequisites:

- `az login` as a member of the **`Gatherstead SQL Admins`** group (or any identity already granted access
  via `infrastructure/ci-grant.sql`).
- `dotnet tool install --global dotnet-ef --version 10.0.9`
- An Azure SQL firewall rule allowing your current IP (the DB has no public access by default):

  ```bash
  MY_IP=$(curl -fsSL https://api.ipify.org)
  az sql server firewall-rule create --resource-group <resource-group> --server <sql-server-name> \
    --name "manual-migrate" --start-ip-address "$MY_IP" --end-ip-address "$MY_IP"
  ```

**Option A — apply migrations directly** (override the dummy connection with `--connection`):

```bash
dotnet ef database update --project src/Gatherstead.Data \
  --connection "Server=tcp:<sql-server-fqdn>,1433;Database=gatherstead;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
```

**Option B — mirror CI (recommended)** — generate an idempotent script offline (no DB connection needed),
then apply it with sqlcmd. This is exactly what the pipeline does in `.github/workflows/ci-cd.yml`:

```bash
dotnet ef migrations script --idempotent --project src/Gatherstead.Data -o migrate.sql
sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault \
  --exit-on-error -l 60 -i migrate.sql
```

CI and any in-Azure execution use the managed-identity form of the connection string instead, since the
identity is available there:

```
Server=tcp:<sql-server-fqdn>,1433;Database=gatherstead;Authentication=Active Directory Managed Identity;User Id=<managedIdentityClientId>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Remember to remove the temporary firewall rule when finished:

```bash
az sql server firewall-rule delete --resource-group <resource-group> --server <sql-server-name> --name "manual-migrate"
```

### 5. Configure Encryption and Temporal Retention

**Run this manually as a SQL admin — it is deliberately not part of CI.** Wrapping the CEK requires
**Key Vault Crypto User** access on the vault, which the CI identity does not have, and the setup is
driven by the `Gatherstead.Data.Setup` tool rather than a raw script (it must generate/wrap the CEK and
encrypt temporal columns). Run it as an identity in the **`Gatherstead SQL Admins`** group (which has
`CONTROL`) that also has **Key Vault Crypto User** on the vault. It is idempotent, so re-run it whenever
PII columns are added or changed; routine schema migrations (step 4) remain automated in CI.

Authenticate with `az login` as that admin, then run the setup utility with the CMK's **Key Vault key
URL** as the second argument. This must be the key's `https://<vault>.vault.azure.net/keys/...`
identifier — **not** the ARM resource ID. (The Bicep `cmkKeyId` output / `keyVaultCmkId` in
`infrastructure/output.json` is the ARM resource ID and will fail with
`Invalid url specified ... (Parameter 'masterKeyPath')`.) Get the correct value with:

```bash
az keyvault key show --vault-name <vault-name> --name cmk-gatherstead --query key.kid -o tsv
# e.g. https://kv-gat-prod-wus2-a1b2c3.vault.azure.net/keys/cmk-gatherstead
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

### Configure External Identity Sign-in

Both apps authenticate users against **Microsoft Entra External ID** (`ciamlogin.com`). The API validates
JWT bearer tokens (config injected as `ExternalIdentity__*` app settings); the Nuxt web app runs the
server-side OIDC authorization-code + **PKCE** flow ([server/routes/auth/azure.get.ts](../src/Gatherstead.Web/server/routes/auth/azure.get.ts)),
storing the access token only in an encrypted server session. The code is redeemed **server-side**, so the
web app is a **confidential client**: it must present a **client secret** to redeem the code (a SPA-style
PKCE-only redemption fails server-side with `AADSTS9002327` — "may only be redeemed via cross-origin
requests"). Both web secrets — the client secret and the session-encryption key — are kept in Key Vault.
One-time setup after provisioning:

1. **Create the Nuxt session secret in Key Vault** (resolved at runtime via the web app's managed identity
   through the `NUXT_SESSION_PASSWORD` Key Vault reference):
   ```bash
   az keyvault secret set --vault-name <vault-name> --name nuxt-session-password \
     --value "$(openssl rand -base64 48)"   # any random string ≥ 32 chars
   ```
2. **API app registration** — under *Expose an API*, add a scope (e.g. `access_as_user`) and set the
   Application ID URI. This is the `webExternalIdentityApiScope` value (`api://<api-client-id>/access_as_user`)
   so the web app's access token is audienced for the API.
3. **Web app registration** — under *Authentication → Platform configurations*, register the redirect URI
   `https://<webAppUrl>/auth/azure` under a **Web** platform (**not** Single-page application — a SPA
   registration forces a browser cross-origin redemption and rejects the server-side exchange with
   `AADSTS9002327`/HTTP 400). Then under *Certificates & secrets* create a **client secret**, and store it
   in Key Vault so the app setting resolves it:
   ```bash
   az keyvault secret set --vault-name <vault-name> --name web-external-identity-client-secret \
     --value "<the client secret value>"
   ```
   Grant the registration delegated permission to the API scope from step 2. Its client ID is
   `webExternalIdentityClientId`.
4. Fill the `externalIdentity*` and `webExternalIdentity*` values in `prod.bicepparam` (tenant, client IDs,
   issuer) and redeploy so the app settings pick them up. The web App Service should then show both the
   `NUXT_SESSION_PASSWORD` and `NUXT_EXTERNAL_IDENTITY_CLIENT_SECRET` Key Vault references as **Resolved**
   in the portal.

#### Enable self-service sign-up (registration)

Registration is handled entirely by Entra External ID — there is **no app code change**. The same `/auth/azure`
sign-in flow surfaces a "No account? Create one" link once self-service sign-up is enabled on the user flow the
web app registration is attached to. In the **Microsoft Entra admin center** (external tenant
`gatherstead.onmicrosoft.com`):

1. **External Identities → User flows** — create or edit the **sign-up and sign-in** user flow, and under its
   **Applications** add the web app registration (`webExternalIdentityClientId`). This is what makes its
   `/authorize` requests offer sign-up.
2. Set the flow's **identity providers** to include **Email with password** so users can self-register, and keep
   **email one-time-passcode verification required** — every account proves mailbox control at sign-up. That is the
   trust basis for auto-claiming invitations by email: the API trusts the validated issuer's `email` claim (Entra
   External ID does not emit a per-token `email_verified` for these accounts), blocking only an explicit unverified
   signal (see below).
3. **Abuse baseline — enable the flow's built-in CAPTCHA** to block scripted bot signups. This is the primary
   mitigation for open sign-up; the API layer already provides defence-in-depth: bootstrap (`POST /api/me/bootstrap`)
   is JWT-gated so a `User` row can only be written after a real sign-up, provisioning is idempotent per `ExternalId`
   (one identity = one row), invitations auto-claim only against the validated issuer's email and are blocked if the
   IdP explicitly marks it unverified (a bogus account lands group-less with no access), and a per-IP rate limit applies. **Known residual gaps, intentionally not addressed
   here:** no WAF/Front Door in front of the App Services, no anomaly alerting on User-creation rate, and no cleanup
   job for orphaned zero-tenant users — the residual risk is junk-row accumulation, not unauthorized access.

#### Return the Display Name claim (required for `User.DisplayName`)

The API seeds a user's editable `User.DisplayName` (shown/edited on **Settings → Account**) from the token
`name` claim at first login. *Collecting* the Display Name attribute on the user flow is not enough — the flow
must also **return** it as a token claim:

1. **External Identities → User flows → sign-up and sign-in → User attributes** — ensure **Display Name** is
   collected, and under **Application claims** (the claims the token returns) tick **Display Name** so it is
   emitted as the `name` claim.
2. The backend reads the claim from the **access token** presented to `POST /api/me/bootstrap` (the same way it
   reads the `email` claim). Confirm `name` is present there. If a deployment only emits `name` on the
   id_token, the seed will be empty and the user can still set their name manually on the Account page.

This claim list is configured **in the portal only** — it is not represented in the Bicep under `infrastructure/`,
which carries the auth *parameters* (`Instance`, `Domain`, `ClientId`, `ValidIssuer`) but not the user-flow
attribute/claim selection.

`SignUpSignInPolicyId` stays **empty** throughout — it is an Azure AD B2C concept; Entra External ID uses the
tenant's user flow instead, not a policy ID in the authority URL.

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

`infrastructure/modules/staticwebapp.bicep` provisions the Static Web App resource, gated behind a `deployDemo` parameter in `main.bicep` so it is only created when explicitly enabled. The same `deployDemo` flag also provisions a separate App Insights component (`appi-gat-demo-*`, in `observability.bicep`) so anonymous demo traffic never mixes with prod product metrics.

### CI/CD

The `deploy-demo` job in [.github/workflows/ci-cd.yml](../.github/workflows/ci-cd.yml) builds and deploys the demo on push to `main` (after build + test + `deploy-api` pass), keeping it in sync with the latest code.

### Frontend telemetry

Browser telemetry uses the App Insights JS SDK (see [OBSERVABILITY.md](OBSERVABILITY.md#frontend-telemetry)), delivered via `NUXT_PUBLIC_APPINSIGHTS_CONNECTION_STRING`:

- **Prod** — set automatically as a web-app app setting in `appservice.bicep` (points at the shared `appi-gat-*`, enabling frontend↔backend trace correlation).
- **Demo** — copy the `demoAppInsightsConnectionString` deployment output into the `DEMO_APPINSIGHTS_CONNECTION_STRING` GitHub Actions secret; the `deploy-demo` job in `ci-cd.yml` bakes it into the static build at `pnpm generate` time. It is an ingestion-only key and safe to expose.

See [DEMO_SITE.md](agents/plans/DEMO_SITE.md) for full architecture and implementation details.

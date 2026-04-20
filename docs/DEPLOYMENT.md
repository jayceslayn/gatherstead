# Database Encryption and Deployment

This project uses Always Encrypted with Secure Enclaves to protect sensitive data. Infrastructure is managed with **Bicep** (Azure-native IaC) and all resource-to-resource authentication uses **managed identity** — no passwords or connection string secrets.

## Prerequisites

- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) with Bicep extension: `az bicep install`
- Contributor + User Access Administrator roles on the target subscription
- .NET 10 SDK

## CI gates before deploy

Every PR to `main` must pass four jobs in [.github/workflows/build-and-test.yml](../.github/workflows/build-and-test.yml):

- **`build-backend`** — `dotnet restore --locked-mode`, build, test.
- **`build-frontend`** - `pnpm build`
- **`audit-nuget`** — fails on any vulnerable NuGet package (direct or transitive).
- **`audit-pnpm`** — fails on any `high`+ severity pnpm advisory.
- **`dependency-review`** — blocks PRs that introduce a known-vulnerable dependency.

Lockfiles (`packages.lock.json` per .NET project, `pnpm-lock.yaml` for the web app) are committed and integrity-checked on every build. Emergency security patches follow the runbook in [SECURITY-DEPS.md](SECURITY-DEPS.md#emergency-patch-runbook) and still deploy via this same pipeline.

## Infrastructure Structure

```
infrastructure/
  main.bicep               # Subscription-scoped root: resource group, wires modules together
  modules/
    identity.bicep         # User-assigned managed identity for the application
    keyvault.bicep         # Key Vault (Premium) + CMK key + RBAC role assignments
    sql.bicep              # SQL Server (Entra ID-only auth) + Database
    appservice.bicep       # App Service Plan + API app (.NET 10) + Web app (Node 24)
  parameters/
    dev.bicepparam         # Dev: F1 (free) App Service Plan
    prod.bicepparam        # Prod: B1 (basic) App Service Plan
  encrypt-columns.sql      # Applies Always Encrypted to sensitive columns (one-time)
  post-deploy.sql          # Grants the managed identity SQL database access (one-time)
```

## SKU Differences: Dev vs Prod

| | F1 (Free) — dev | B1 (Basic) — prod |
|---|---|---|
| Cost | $0 | ~$13/month/plan |
| CPU | 60 min/day shared | 1 core dedicated |
| RAM | 1 GB shared | 1.75 GB |
| Always On | No | Yes |
| Custom domains | No | Yes |

Both apps (API + Web) share one plan. Scale up `appServicePlanSku` in `prod.bicepparam` as traffic grows (B2, B3, P1v3, etc.).

## Deployment Workflow

### 1. Configure Parameters

Edit `infrastructure/parameters/dev.bicepparam` (or `prod.bicepparam`) and fill in:

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
  --location eastus \
  --template-file infrastructure/main.bicep \
  --parameters infrastructure/parameters/dev.bicepparam
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

### 4. Run EF Core Migrations

```bash
dotnet ef database update --project src/Gatherstead.Data
```

The connection string must use Entra ID managed identity auth:

```
Server=tcp:<sql-server-fqdn>,1433;Database=gatherstead;Authentication=Active Directory Managed Identity;User Id=<managedIdentityClientId>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### 5. Configure Encryption Keys and Temporal Retention

Run the setup utility with the managed identity connection string and the CMK Key Vault ID:

```bash
dotnet run --project src/Gatherstead.Data.Setup/Gatherstead.Data.Setup.csproj -- \
  "<managed-identity-connection-string>" \
  "<keyVaultCmkId>"
```

This creates the Column Master Key (CMK) and Column Encryption Key (CEK) metadata in the database, and sets a 1-year retention policy on all temporal history tables.

The setup utility uses `DefaultAzureCredential`, so run it in an environment where the managed identity (or your own Entra ID identity) has Key Vault Crypto User access.

### 6. Encrypt Columns

Using `sqlcmd` or SSMS (connected as the Entra ID SQL admin), execute the entire contents of `infrastructure/encrypt-columns.sql`. This is a one-time operation that applies Always Encrypted to sensitive columns.

### 7. Deploy the Application

The API and Web App Service apps are already provisioned with the correct configuration (managed identity, connection string, Key Vault URI, CORS, API base URL). Deploy code to them using zip deploy or CI/CD:

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
cd .output && zip -r ../../../web.zip .
az webapp deploy --resource-group <resourceGroupName> --name <webAppName> --src-path web.zip
```

After deployment, the apps are reachable at the `apiAppUrl` and `webAppUrl` outputs.

## Demo Site (Planned)

A public demo site will be deployed alongside production on **Azure Static Web Apps (Free tier)** at `demo.gatherstead.<host>.<ext>`. The demo uses the same codebase but is built as a static SPA with `nuxt generate` and `NUXT_PUBLIC_DEMO_MODE=true`, using browser localStorage instead of a backend API. No App Service plan, SQL Server, or managed identity is required — zero hosting cost.

### Infrastructure

A new `infrastructure/modules/staticwebapp.bicep` module provisions the Static Web App resource, gated behind a `deployDemo` parameter in `main.bicep` so it is only created when explicitly enabled.

### CI/CD

A dedicated GitHub Actions workflow (`.github/workflows/deploy-demo.yml`) builds and deploys the demo on push to `main`, keeping it in sync with the latest code.

See [DEMO_SITE.md](agents/plans/DEMO_SITE.md) for full architecture and implementation details.

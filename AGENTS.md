# Agent Guidelines for Gatherstead

This repository uses Azure-first architecture with a C# .NET API and a Vue 3 / Nuxt 3 web UI. Keep changes aligned with these directions and the domain principles in `README.md` and `STRATEGY.md`.

## Expectations for Automated Changes
- Favor Azure-native services (App Service/Functions, Azure SQL/Cosmos DB, Azure Storage, Key Vault, Event Grid/Service Bus) when proposing infrastructure or integrations.
- API layer is C# .NET: follow ASP.NET Core conventions, strong typing, and dependency injection. Avoid adding non-.NET API stacks.
- Web UI layer is Vue 3 with Nuxt 3: use composition API and Nuxt conventions for routing, server routes, and state.
- Preserve multi-tenant and privacy-by-design principles highlighted in `STRATEGY.md` when touching domain logic.
- Keep secrets out of source; assume Key Vault or Azure-managed identities for sensitive configuration.
- Prefer incremental, testable changes with clear separation between backend and frontend packages.

### Infrastructure defaults
- Treat Azure as the primary target: prefer Bicep/ARM over ad-hoc CLI scripting for IaC, and design for App Service/Functions with Managed Identity and Key Vault integration for secrets.
- Plan observability from the start: include App Insights/Log Analytics hooks, structured logs, and dashboards/alerts for auth failures, data-access anomalies, and PII access patterns.
- Default to private networking (VNet integration, private endpoints) for data stores; avoid exposing databases or storage publicly.
- Assume multiple environments (dev/test/prod). Keep configuration in App Configuration/Key Vault and avoid environment-specific code.
- For storage and databases, enforce tenant scoping and indexing that match the domain guidance in `STRATEGY.md`.
- The current SQL implementation targets SQL Server; prefer SQL Server-friendly defaults and tooling when wiring up the data layer.

### Backend conventions
- Use ASP.NET Core dependency injection, nullable reference types, and async APIs. Prefer minimal APIs or controllers consistent with existing style, and keep DTOs separate from EF entities.
- Favor Entity Framework Core migrations for schema changes; keep migrations deterministic and seed data idempotent.
- Validate inputs (model validation attributes/FluentValidation), enforce authorization per-tenant, and log audit events for sensitive operations.

### Frontend conventions
- Use Nuxt 3 conventions (pages, server routes, composables) with the Vue 3 composition API and TypeScript. Keep shared state in composables or Pinia when appropriate.
- Follow accessibility best practices and align UI copy with the family-planning domain.
- Manage secrets via runtime config/environment variables, not hardcoded constants. Respect multi-tenant boundaries in any client-side routing or data fetching.

## Repository Notes
- Root docs (`README.md`, `STRATEGY.md`) describe product vision and domain boundaries—consult them before altering domain models.
- No existing formatting guardrails are enforced here; follow language-idiomatic styles (e.g., `dotnet format` for C#, ESLint/Prettier defaults for Vue/Nuxt where applicable).

## PR / Review Guidance
- Summaries should call out Azure alignment, C# backend, and Vue/Nuxt frontend impacts when relevant.
- Include test commands run for the affected layer (e.g., `dotnet test`, `npm test`, or `npm run lint`) when applicable.

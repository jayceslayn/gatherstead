# Gatherstead Architecture

## Technology Stack
This repository uses an Azure-first architecture with a C# .NET API and a Vue 3 / Nuxt 3 web UI. Keep changes aligned with these directions.

## Domain-Driven Design Overview
Gatherstead is organized around bounded contexts that align with the two core goals while sharing a multi-tenant foundation.

### Shared Foundation (Tenancy)
- **Tenant**: Top-level aggregate for an extended family or organization. Tenants own households, properties, events, and users, providing isolation across families or groups.【F:src/Gatherstead.Data/Entities/Tenant.cs】

### Family Directory Context
- **Household**: Represents a family grouping under a tenant and owns its members.【F:src/Gatherstead.Data/Entities/Household.cs】 Households can evolve as families split or merge.
- **HouseholdMember**: Person-centric record that stores name, birth date, dietary notes/tags, and adult/child markers, with Always Encrypted columns for sensitive data.【F:src/Gatherstead.Data/Entities/HouseholdMember.cs】 This is the anchor for relationships to gatherings, meals, and lodging intents.
- **Relationships**: Parent/child/sibling/spouse links will be modeled as member-to-member relationships to keep lineage flexible; this is a planned enhancement beyond current entities.
- **Contact & extensible details**: Additional value objects (addresses, phone/email, custom attributes) can be attached to HouseholdMember in future iterations to fulfill the directory goal.

### Gathering Planning Context
- **Property**: Physical location that can host events and belongs to a tenant.【F:src/Gatherstead.Data/Entities/Property.cs】
- **Event**: Time-bounded gathering tied to a tenant and property; aggregates resources, meal plans, and chore templates for the event window.【F:src/Gatherstead.Data/Entities/Event.cs】
- **Resource**: Lodging or facility slot (e.g., guest room, RV space) with capacities and notes; collects stay intents from members instead of hard reservations.【F:src/Gatherstead.Data/Entities/Resource.cs】
- **StayIntent**: Member's request to use a resource on a given night, with status and notes for offline arbitration.【F:src/Gatherstead.Data/Entities/StayIntent.cs】
- **MealPlan**: Defines meals for specific days within an event and aggregates meal intents and notes.【F:src/Gatherstead.Data/Entities/MealPlan.cs】
- **MealIntent**: Member-level response indicating attendance for a meal, dietary considerations, and bring-your-own-food choices.【F:src/Gatherstead.Data/Entities/MealIntent.cs】
- **ChoreTemplate**: Volunteer-friendly template for recurring chores (by time slot) across an event.【F:src/Gatherstead.Data/Entities/ChoreTemplate.cs】
- **ChoreTask**: Dated chore assignments created from templates, allowing optional meal alignment, multiple assignees, completion tracking, and notes.【F:src/Gatherstead.Data/Entities/ChoreTask.cs】

## Technology Conventions

### Infrastructure
- Treat Azure as the primary target: prefer Bicep/ARM over ad-hoc CLI scripting for IaC, and design for App Service/Functions with Managed Identity and Key Vault integration for secrets.
- Plan observability from the start: include App Insights/Log Analytics hooks, structured logs, and dashboards/alerts for auth failures, data-access anomalies, and PII access patterns.
- Default to private networking (VNet integration, private endpoints) for data stores; avoid exposing databases or storage publicly.
- Assume multiple environments (dev/test/prod). Keep configuration in App Configuration/Key Vault and avoid environment-specific code.
- For storage and databases, enforce tenant scoping and indexing that match the domain guidance in [DESIGN_PRINCIPLES.md](DESIGN_PRINCIPLES.md).
- The current SQL implementation targets SQL Server; prefer SQL Server-friendly defaults and tooling when wiring up the data layer.

### Backend
- Use ASP.NET Core dependency injection, nullable reference types, and async APIs. Prefer minimal APIs or controllers consistent with existing style, and keep DTOs separate from EF entities.
- Favor Entity Framework Core migrations for schema changes; keep migrations deterministic and seed data idempotent.
- Validate inputs (model validation attributes/FluentValidation), enforce authorization per-tenant, and log audit events for sensitive operations.
- **API endpoint design**: List/read endpoints should support batch filtering via query parameters (e.g., `?ids=aaa,bbb`) to reduce client round-trips. Keep create, update, and delete endpoints singular; introduce workflow-specific batch write endpoints only when concrete use cases demand them (e.g., bulk event setup).

### Frontend
- Use Nuxt 3 conventions (pages, server routes, composables) with the Vue 3 composition API and TypeScript. Keep shared state in composables or Pinia when appropriate.
- Follow accessibility best practices and align UI copy with the family-planning domain.
- Manage secrets via runtime config/environment variables, not hardcoded constants. Respect multi-tenant boundaries in any client-side routing or data fetching.

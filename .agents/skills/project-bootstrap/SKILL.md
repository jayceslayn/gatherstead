---
name: project-bootstrap
description: Start here before any task. Provides project orientation and routes agents to the right docs by task type, replacing broad codebase exploration.
---

# Gatherstead — Project Bootstrap

## Quick Facts

| | |
|---|---|
| **Stack** | C# .NET 10 API · Vue 3 / Nuxt 4 frontend · Azure SQL |
| **Auth** | Microsoft Entra ID (live) · demo mode bypasses auth entirely |
| **PII protection** | Always Encrypted columns for sensitive fields |
| **Multi-tenancy** | Every entity carries `TenantId`; global EF Core query filters enforce isolation |
| **Soft delete** | All entities use `IsDeleted` / `DeletedAt` — never hard-delete |

## Build & Lint Gates

```
# Backend (run from repo root)
dotnet build Gatherstead.sln          # must: 0 errors, 0 warnings
dotnet test Gatherstead.sln           # must: all pass

# Frontend (run from src/Gatherstead.Web/)
pnpm build
pnpm run lint
```

## Directory Map

```
src/
  Gatherstead.Api/          C# .NET 10 API — controllers, middleware, auth
  Gatherstead.Data/         EF Core — DbContext, interceptors, migrations
  Gatherstead.Domain/       Domain entities and shared types
  Gatherstead.Tests/        Unit + integration tests
  Gatherstead.Web/          Nuxt 4 frontend
    app/
      composables/          Data composables (use repository pattern — see below)
      pages/                App routes
      stores/               Pinia stores (tenant, member, event context)
      repositories/         Live + demo repository implementations
      plugins/              repositories.client.ts wires live vs. demo at startup
      middleware/           tenant.global.ts — sets tenant context on /app/* routes
    i18n/locales/           en.json, es.json — all UI strings live here
docs/
  ARCHITECTURE.md           Tech stack, entity hierarchy, backend + frontend conventions
  DESIGN_PRINCIPLES.md      Security, privacy, tenant isolation, data lifecycle rules
  IMPLEMENTATION_STATUS.md  What exists today; schema detail; planned enhancements
  DEPLOYMENT.md             Bicep infrastructure, deployment runbook, SKU differences
  OBSERVABILITY.md          PII logging rules, OTel conventions, allowlist
  agents/plans/             Detailed implementation plans for upcoming features
.agents/skills/             Agent skill files (this directory)
```

## Task-Type Routing

Read only what your task needs. Skip the rest.

| Task type | Read these | Key facts inside |
|-----------|-----------|-----------------|
| **Backend (.NET)** | `docs/ARCHITECTURE.md` + `docs/IMPLEMENTATION_STATUS.md` | Controller patterns, EF conventions, FK layout, what's implemented |
| **Frontend (Vue/Nuxt)** | `docs/ARCHITECTURE.md` (frontend section) + `docs/agents/plans/WEB-UI-DESIGN.md` | Composable patterns, repository injection, page structure, i18n |
| **Planning / design** | `docs/ARCHITECTURE.md` + browse `docs/agents/plans/` for format precedent | Existing plan structure; scope conventions |
| **Security / observability** | `docs/DESIGN_PRINCIPLES.md` + `docs/OBSERVABILITY.md` | PII logging ban, tenant isolation rules, OTel allowlist |
| **Deployment / infra** | `docs/DEPLOYMENT.md` | Bicep modules, SKU differences, migration runbook |

## Non-Negotiable Conventions

- **Never log PII field values** — log entity IDs only (`docs/OBSERVABILITY.md`)
- **Tenant isolation** — every query must be scoped to `TenantId`; EF query filters handle it automatically; do not bypass them
- **Soft delete** — set `IsDeleted = true`, never `DELETE FROM`
- **Frontend data access** — composables call `useRepositories()` (injected via plugin), never `$fetch` directly
- **Demo mode** — `config.public.demoMode` is the single flag; repositories handle live vs. demo transparently; middleware and plugin handle tenant/member bootstrapping

---

*To add a task-specific skill, create `.agents/skills/<name>/SKILL.md` with `name` and `description` frontmatter, then add a row to the routing table above.*

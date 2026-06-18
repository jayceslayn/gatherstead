# Agent Guidelines for Gatherstead
This repository uses Azure-first architecture with a C# .NET 10 API and a Vue 3 / Nuxt 4 web UI. For a comprehensive overview of the project's architecture, design principles, and implementation status, please refer to the documentation in the `docs/` directory.

## Working Style

### Planning
- Load as much context as required, but as little as necessary — target your reads rather than broad codebase exploration.
- Search for existing functions, utilities, and patterns and reuse them before proposing new code; avoid duplication.
- Resolve open architecture questions and likely pitfalls *before* drafting a detailed implementation plan, and make key assumptions explicit so the user can correct them.
- Batch independent reads and searches into a single message; delegate broad fan-out searches to subagents and relay only their conclusions to keep the main context lean.
- Stay within the requested scope — surface tangential issues for the user to decide rather than fixing them inline.
- Define explicit stop signals: conditions under which implementation should halt and report back because reality is diverging from the plan (e.g. expected APIs/utilities are absent, tests invalidate a core assumption, or scope expands beyond the plan).
- Include verification steps and unit tests in the plan wherever possible.

### Implementation
- Honour the stop signals defined during planning — pause and surface the divergence instead of pressing on.
- Write code that reads like the surrounding code — match naming, idioms, and comment density rather than introducing new styles.
- Verify as you go; do not defer all checking to the end.
- Trust harness state: don't re-read files you just edited to confirm a change, and don't re-derive facts already established in the conversation.
- Report outcomes faithfully: if tests fail, show the output; note skipped steps; don't claim work is done without verification.

### Efficiency (context & output)
- Prefer targeted tools, diffs, and patches over loading or rewriting whole files. Use search to find the minimal relevant span and edit in place rather than regenerating files wholesale.
- For larger files or inputs, consider generating a deterministic script to parse or modify them rather than reading them in full.
- In internal reasoning and user-facing discussion, use clear, dense, precise, technical-leaning language. Avoid unnecessary verbosity.

## Project Documentation
Before making changes, consult the following documentation to understand the project's vision, architecture, and conventions:

- **[README.md](README.md)**: The main project readme with an introduction to the project's goals and vision.
- **[Architecture](docs/ARCHITECTURE.md)**: An overview of the technology stack, domain-driven design, and technical conventions.
- **[Design Principles](docs/DESIGN_PRINCIPLES.md)**: The guiding principles for security, privacy, and data safety.
- **[Implementation Status](docs/IMPLEMENTATION_STATUS.md)**: The current implementation status, planned enhancements, and schema details.
- **[Deployment Guide](docs/DEPLOYMENT.md)**: Detailed instructions for manual and automated deployment.

## PR / Review Guidance
- Summaries should call out Azure alignment, C# backend, and Vue/Nuxt frontend impacts when relevant.
- Backend: run `dotnet build Gatherstead.sln` (must produce 0 errors, 0 warnings) and `dotnet test Gatherstead.sln` (all tests must pass) before marking work complete.
- Frontend: run `pnpm build` and `pnpm run lint` from `src/Gatherstead.Web/`.

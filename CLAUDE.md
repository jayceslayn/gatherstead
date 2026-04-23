# Agent Guidelines for Gatherstead
This repository uses Azure-first architecture with a C# .NET 10 API and a Vue 3 / Nuxt 4 web UI. For a comprehensive overview of the project's architecture, design principles, and implementation status, please refer to the documentation in the `docs/` directory.

## Agent Skills
Read `.agents/skills/project-bootstrap/SKILL.md` at the start of every task before exploring the codebase.
It provides quick facts, a directory map, task-type routing to the right docs, and the non-negotiable conventions — use it to target what to read next rather than broad codebase exploration.

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

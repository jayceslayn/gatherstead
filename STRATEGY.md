# Gatherstead Strategy and Domain Map

## Vision and Goals
- **Family details**: Maintain canonical records of each person's current name, birth date, family relationships, contact details, dietary needs/preferences, and other extensible attributes. Individuals and guardians/admins should be able to edit these details.
- **Gathering planning**: Plan events with date ranges, attendance by day/meal, meal prep assignments, chore duties, and lodging usage that supports flexible, arbitrated requests rather than hard reservations.
- **Evolution over time**: Support family groupings that can change as children form their own households, while keeping history and relationships coherent.
- **Extensibility**: Allow additional goals and modules to attach without disrupting existing domains.

## Domain-Driven Design Overview
Gatherstead is organized around bounded contexts that align with the two core goals while sharing a multi-tenant foundation.

### Shared Foundation (Tenancy)
- **Tenant**: Top-level aggregate for an extended family or organization. Tenants own households, properties, events, and users, providing isolation across families or groups.【F:packages/db/Entities/Tenant.cs†L7-L14】

### Family Directory Context
- **Household**: Represents a family grouping under a tenant and owns its members.【F:packages/db/Entities/Household.cs†L7-L12】 Households can evolve as families split or merge.
- **HouseholdMember**: Person-centric record that stores name, birth date, dietary notes/tags, and adult/child markers, with encrypted fields for sensitive data.【F:packages/db/Entities/HouseholdMember.cs†L7-L22】 This is the anchor for relationships to gatherings, meals, and lodging intents.
- **Relationships**: Parent/child/sibling/spouse links will be modeled as member-to-member relationships to keep lineage flexible; this is a planned enhancement beyond current entities.
- **Contact & extensible details**: Additional value objects (addresses, phone/email, custom attributes) can be attached to HouseholdMember in future iterations to fulfill the directory goal.

### Gathering Planning Context
- **Property**: Physical location that can host events and belongs to a tenant.【F:packages/db/Entities/Property.cs†L7-L12】
- **Event**: Time-bounded gathering tied to a tenant and property; aggregates resources, meal plans, and chore templates for the event window.【F:packages/db/Entities/Event.cs†L7-L19】
- **Resource**: Lodging or facility slot (e.g., guest room, RV space) with capacities and notes; collects stay intents from members instead of hard reservations.【F:packages/db/Entities/Resource.cs†L7-L16】
- **StayIntent**: Member's request to use a resource on a given night, with status and notes for offline arbitration.【F:packages/db/Entities/StayIntent.cs†L7-L15】
- **MealPlan**: Defines meals for specific days within an event and aggregates meal intents and notes.【F:packages/db/Entities/MealPlan.cs†L7-L16】
- **MealIntent**: Member-level response indicating attendance for a meal, dietary considerations, and bring-your-own-food choices.【F:packages/db/Entities/MealIntent.cs†L7-L15】
- **ChoreTemplate**: Volunteer-friendly template for recurring chores (by time slot) across an event.【F:packages/db/Entities/ChoreTemplate.cs†L7-L16】
- **ChoreTask**: Dated chore assignments created from templates, allowing optional meal alignment, multiple assignees, completion tracking, and notes.【F:packages/db/Entities/ChoreTask.cs†L7-L16】

## Schema Review and Recommended Improvements
The current model covers tenants, households/members, events, meal planning, chores, and lodging intents, giving a solid baseline for the two core goals. Key areas to tighten for fitness and queryability:

- **Lineage and guardianship**: HouseholdMember currently has no explicit relationships; introduce a `MemberRelationship` entity (parent/child/sibling/spouse/guardian) to fulfill the family-tree requirement and support permissions that respect guardians.【F:packages/db/Entities/HouseholdMember.cs†L7-L22】
- **Contact and extensible profile data**: Add value objects/entities for `ContactMethod` (email, phone), `Address`, and `MemberAttribute` key/value pairs to store emergent details (e.g., preferred pronouns, accessibility needs) without schema churn.【F:packages/db/Entities/HouseholdMember.cs†L7-L22】
- **Dietary structure**: Keep `DietaryTags` for fast filtering, but normalize common allergies/preferences into a `DietaryProfile` table to avoid free-text drift and to enable menu planning analytics.【F:packages/db/Entities/HouseholdMember.cs†L7-L22】
- **Attendance vs. meal-specific intents**: MealIntent captures per-meal RSVP, but there is no day-level presence. Add an `EventAttendance` aggregate for daily presence and arrival/departure windows to better plan capacity and chores, while keeping MealIntent for per-meal nuances.【F:packages/db/Entities/MealIntent.cs†L7-L15】
- **Lodging arbitration metadata**: `StayIntent` should carry arbitration outcomes (approved/declined), priority, and household-size counts to support offline negotiation and fairness rules.【F:packages/db/Entities/StayIntent.cs†L7-L15】
- **Chore assignment normalization**: `ChoreTask` stores assignees as a `Guid[]`, which is hard to query. Replace with a join entity (e.g., `ChoreAssignment`) that links tasks to members, supports multiple assignees, and records volunteered vs. assigned origin.【F:packages/db/Entities/ChoreTask.cs†L7-L16】
- **Auditability and permissions**: Add created/updated timestamps and creator IDs on mutable entities (HouseholdMember, MealIntent, StayIntent, ChoreTask) to support admin review and parental-guardian workflows, aligning with the requirement that guardians/admins can edit individual details.【F:packages/db/Entities/HouseholdMember.cs†L7-L22】【F:packages/db/Entities/StayIntent.cs†L7-L15】【F:packages/db/Entities/MealIntent.cs†L7-L15】【F:packages/db/Entities/ChoreTask.cs†L7-L16】
- **Multi-tenant safety**: Ensure all aggregates enforce tenant scoping (e.g., Event → Tenant, HouseholdMember → Household → Tenant) at the data-access layer with indexes on tenant/foreign keys to keep cross-family isolation efficient.【F:packages/db/Entities/Event.cs†L7-L19】【F:packages/db/Entities/Household.cs†L7-L12】

## Implementation Status and Next Steps
- **Implemented**: Multi-tenant backbone, households and members with dietary metadata, event scaffolding with meal planning, chore planning, and lodging intent structures are represented in the data model.【F:packages/db/Entities/Tenant.cs†L7-L14】【F:packages/db/Entities/HouseholdMember.cs†L7-L22】【F:packages/db/Entities/Event.cs†L7-L19】【F:packages/db/Entities/Resource.cs†L7-L16】【F:packages/db/Entities/MealPlan.cs†L7-L16】【F:packages/db/Entities/ChoreTemplate.cs†L7-L16】
- **Planned**: Member relationship graph, contact information value objects, permissions/admin workflows for guardians, event attendance summaries, chore sign-up flows, and arbitration flows for lodging requests.
- **Architecture direction**: Treat households and events as separate aggregates linked through member IDs, enabling independent evolution of directory data and event participation while maintaining traceability.

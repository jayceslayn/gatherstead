# Gatherstead

Gatherstead keeps sprawling families organized and together: one source of truth for everyone’s details, and one place to plan every shared weekend, holiday, or reunion.

## Why Gatherstead
- **Lead with togetherness**: Make it easy for any relative to step in and coordinate—no more scattered spreadsheets or phone trees.
- **Ship a calmer gathering**: Turn headcounts, dietary needs, lodging preferences, and chore plans into actionable lists before anyone arrives.
- **Respect every household**: Multi-tenant by design so each family’s data stays private, with consent and lifecycle controls baked in.

## Vision
- **Family details**: Maintain canonical records of each person's current name, birth date, family relationships, contact details, dietary needs/preferences, and other extensible attributes. Individuals and guardians/admins should be able to edit these details.
- **Gathering planning**: Plan events with date ranges, attendance by day/meal, meal prep assignments, chore duties, and lodging usage that supports flexible, arbitrated requests rather than hard reservations.
- **Evolution over time**: Support family groupings that can change as children form their own households while keeping history and relationships coherent.
- **Extensibility**: Allow additional goals and modules to attach without disrupting existing domains.

## Primary Use Cases
- **Centralized family directory**: Keep up-to-date contact information (emails, phone numbers, mailing addresses) and relationship context for everyone in the extended family, making it easy to send updates, invitations, or holiday cards.
- **Dietary and accessibility notes**: Track dietary tags/preferences and other important notes (e.g., accessibility needs) so hosts can plan inclusive meals.
- **Event attendance and meals**: Aggregate who is attending which dates and meals, capture bring-your-own-food choices, and surface headcounts for shopping and prep.
- **Lodging coordination**: Collect stay intents for guest rooms, RV spots, or other resources, with arbitration-friendly workflows instead of first-come reservations.
- **Chore planning**: Create chore templates and tasks, assign or volunteer for time slots, and track completion during the event.

## Guiding Principles (from STRATEGY.md)
- **Privacy by design**: Treat personal data as sensitive by default, minimize replication, and favor references over denormalized copies.
- **Tenant isolation**: Every query and index should scope data by tenant to prevent cross-family leakage.
- **Secure-by-default implementation**: Use TLS, encrypt sensitive fields, validate inputs, enforce least-privilege roles, and capture audit trails for sensitive changes.
- **Data lifecycle & consent**: Track consent for sharing details and support export/delete workflows per tenant.
- **Operational readiness**: Monitor for anomalous access patterns, keep dependencies updated, and include security reviews in change management.

## Implementation Snapshot
- **Shared foundation**: Tenants own households, properties, events, and users to keep families isolated.
- **Family directory context**: Households group members; member records store names, birth dates, dietary notes/tags, and will expand with relationships, contact methods, and custom attributes.
- **Gathering planning context**: Events tie to properties and manage meal plans, chores, lodging resources, and member intents for attendance, meals, stays, and chores.
- **Planned enhancements**: Member relationship graphs, richer contact/address data, daily attendance summaries, chore sign-up flows, arbitration metadata for lodging, and audit trails across mutable entities.

## Encryption configuration
- **Azure Key Vault first**: Provide `Encryption:KeyVault:Uri` with a managed identity that can read the secret. By default, the API reads the `Encryption:KeyVault:SecretName` (`app-encryption-key`) and optional `SecretVersion`. Rotate keys by creating a new secret version and updating configuration to point to the desired version during rollout.
- **Key material requirements**: Secrets must resolve to a 256-bit (32-byte) key. Use Base64-encoded bytes or a 32-character UTF-8 string; shorter or longer values are rejected.
- **Local development**: When Key Vault isn’t available, set `Encryption:Key` to a 32-byte value in user secrets or environment variables. This path is only permitted when `ASPNETCORE_ENVIRONMENT` is `Development`; production runs must use Key Vault.

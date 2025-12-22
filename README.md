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

## Database Encryption and Deployment
This project uses Always Encrypted with Secure Enclaves to protect sensitive data. The setup and deployment are managed through an automated, Infrastructure as Code (IaC) workflow.

### Automated Deployment Workflow
The end-to-end process is designed to be run from a CI/CD pipeline:

1.  **Provision Infrastructure:** Run the Terraform script in the `infrastructure/` directory. This will provision all necessary Azure resources, including the Key Vault, the master key, the SQL server, and the database.
2.  **Run EF Core Migrations:** The application's deployment pipeline should run standard Entity Framework Core migrations to create the database schema.
3.  **Configure Database Keys:** Execute the `Gatherstead.Db.Setup` utility, passing in the database connection string and the Key Vault CMK URI (both are outputs from the Terraform script). This utility connects to the database and creates the necessary Column Master Key and Column Encryption Key metadata.
4.  **Encrypt Columns:** Execute the `infrastructure/encrypt-columns.sql` script against the database. This script alters the table columns to apply encryption.
5.  **Deploy Application:** Deploy the application itself.

### Manual Setup Steps

To run this process locally or for the first time:

1.  **Provision Infrastructure:**
    *   Navigate to the `infrastructure/` directory.
    *   Initialize Terraform: `terraform init`
    *   Apply the configuration: `terraform apply`
    *   You will be prompted for the SQL admin username and password. Provide secure credentials.
    *   Note the outputs from the Terraform script, especially the `sql_server_name`, `key_vault_uri`, and `key_vault_cmk_id`.

2.  **Run Database Migrations:**
    *   Ensure your `appsettings.Development.json` has the correct connection string for the newly created database.
    *   From the root of the project, run `dotnet ef database update --project packages/db`.

3.  **Configure Encryption Keys:**
    *   Build the setup utility: `dotnet build Gatherstead.sln`
    *   Run the utility with the correct parameters:
        ```bash
        dotnet run --project packages/db.setup/Gatherstead.Db.Setup.csproj -- "<your-connection-string>" "<your-key-vault-cmk-id>"
        ```
    *   Replace `<your-connection-string>` with the full database connection string, including your admin credentials.
    *   Replace `<your-key-vault-cmk-id>` with the `key_vault_cmk_id` output from Terraform.

4.  **Encrypt Columns:**
    *   Using a tool like SSMS or `sqlcmd`, connect to the database.
    *   Execute the entire contents of the `infrastructure/encrypt-columns.sql` script.

5.  **Run the Application:**
    *   Update your application's connection string to include the necessary Always Encrypted parameters:
        ```json
        "ConnectionStrings": {
          "DefaultConnection": "Server=...;Database=...;...;Column Encryption Setting=Enabled;Attestation Protocol=HGS;"
        }
        ```
    *   You can now run the application, which will be able to transparently query the encrypted data.

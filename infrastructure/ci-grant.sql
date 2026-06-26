-- CI/CD grant script: give the GitHub Actions CI managed identity the database access it
-- needs to apply EF Core migrations. Run against the 'gatherstead' database once, after Bicep
-- deployment provisions the CI identity.
--
-- The CI identity (id-gat-ci-*) applies the idempotent EF Core migration script from ci-cd.yml.
-- That script is not DDL-only: EF emits DML for HasData seed data (InsertData/UpdateData/
-- DeleteData — e.g. the DietaryTags reference rows) and for any migrationBuilder.Sql backfill.
-- So CI needs both DDL and write access:
--   - db_ddladmin  — CREATE/ALTER/DROP tables, indexes, etc.
--   - db_datawriter — INSERT/UPDATE/DELETE for seed data and backfills (all tables).
--
-- Deliberately NOT granted: db_datareader. CI must not be able to SELECT application data.
-- This no-read grant is the load-bearing control for PII confidentiality against a compromised
-- or malicious migration: the CI identity also holds Key Vault Crypto User (so deploy-setup can
-- encrypt columns) and could therefore decrypt ciphertext — but with no read access it cannot
-- fetch the encrypted rows in the first place. (The app's own managed identity gets full data
-- access via post-deploy.sql; that is where application reads/writes happen.)
--
-- Prerequisites:
--   - Connect using Entra ID authentication as the SQL administrator.
--   - Replace <ci-identity-name> with the value from the 'ciIdentityName' Bicep output.
--
-- Example connection (Azure CLI):
--   sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault -i infrastructure/ci-grant.sql

-- Add the CI identity as a database user backed by its Entra ID service principal.
CREATE USER [<ci-identity-name>] FROM EXTERNAL PROVIDER;

-- DDL for the schema, plus write access for HasData seed data and backfills.
ALTER ROLE db_ddladmin ADD MEMBER [<ci-identity-name>];
ALTER ROLE db_datawriter ADD MEMBER [<ci-identity-name>];

-- The idempotent migration script SELECTs __EFMigrationsHistory to find which migrations are
-- already applied. db_datawriter grants INSERT/UPDATE/DELETE but NOT SELECT, and we deliberately
-- withhold db_datareader, so grant read on this one table only.
--
-- Pre-create the table here (matching EF Core's schema) so this GRANT is valid on a fresh DB
-- before the first migration runs. The idempotent EF script's IF OBJECT_ID check skips creation
-- when the table already exists.
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

GRANT SELECT ON [dbo].[__EFMigrationsHistory] TO [<ci-identity-name>];

-- NOTE: Always Encrypted setup (CMK/CEK creation, column encryption, temporal retention) is NOT
-- performed by this identity. Encrypting the system-versioned temporal tables requires toggling
-- SYSTEM_VERSIONING, which needs CONTROL on the tables — and CONTROL implies SELECT, which would
-- break the no-read control above. That setup is run manually by a SQL admin via
-- Gatherstead.Data.Setup instead (see docs/DEPLOYMENT.md), so the CI identity needs no key-management
-- or database-scoped ALTER grants here.

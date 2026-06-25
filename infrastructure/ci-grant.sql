-- CI/CD grant script: give the GitHub Actions CI managed identity the database access it
-- needs to apply EF Core migrations and configure Always Encrypted (the deploy-migrations and
-- deploy-setup jobs). Run against the 'gatherstead' database once, after Bicep deployment
-- provisions the CI identity.
--
-- The CI identity (gat-ci-id-*) applies the idempotent EF Core migration script from ci-cd.yml.
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

-- Always Encrypted setup (deploy-setup job → Gatherstead.Data.Setup).
-- That tool creates the column master/encryption keys, encrypts PII columns, and configures
-- temporal retention. db_ddladmin covers ALTER TABLE (column encryption, SYSTEM_VERSIONING) but
-- NOT the key-management or database-level ALTER permissions below, so grant them explicitly:
--   ALTER ANY COLUMN MASTER KEY / ENCRYPTION KEY — CREATE COLUMN MASTER/ENCRYPTION KEY.
--   VIEW ANY ... DEFINITION — read sys.column_master_keys/sys.column_encryption_keys so the
--     idempotency checks see existing keys (and skip re-creating them) and ALTER COLUMN can
--     reference the CEK by name.
--   ALTER (database scope) — ALTER DATABASE CURRENT SET TEMPORAL_HISTORY_RETENTION ON.
-- None of these grant SELECT on application data, so the no-read PII control above still holds:
-- these expose only key *metadata* (the key material lives in Key Vault), not plaintext rows.
GRANT ALTER ANY COLUMN MASTER KEY TO [<ci-identity-name>];
GRANT ALTER ANY COLUMN ENCRYPTION KEY TO [<ci-identity-name>];
GRANT VIEW ANY COLUMN MASTER KEY DEFINITION TO [<ci-identity-name>];
GRANT VIEW ANY COLUMN ENCRYPTION KEY DEFINITION TO [<ci-identity-name>];
GRANT ALTER TO [<ci-identity-name>];

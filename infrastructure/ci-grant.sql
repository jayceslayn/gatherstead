-- CI/CD grant script: give the GitHub Actions CI managed identity the database access it
-- needs to apply EF Core migrations. Run against the 'gatherstead' database once, after Bicep
-- deployment provisions the CI identity.
--
-- The CI identity (id-gat-ci-*) applies the idempotent EF Core migration script from ci-cd.yml.
-- That script is not DDL-only, and DDL/write access alone is not sufficient:
--   - EF emits DML for HasData seed data (InsertData/UpdateData/DeleteData — e.g. the DietaryTags
--     reference rows) and for any migrationBuilder.Sql backfill.
--   - Every AuditableEntity table is a system-versioned temporal table. When a migration drops or
--     alters a column on an existing temporal table (a plain nullable ADD does not), EF wraps the
--     change in ALTER TABLE ... SET (SYSTEM_VERSIONING = OFF) ... SET (SYSTEM_VERSIONING = ON ...).
--     That toggle requires CONTROL on the table and its history table; db_ddladmin/ALTER is NOT enough,
--     and the migration fails with "Msg 13538 ... You do not have the required permissions" (this is a
--     temporal-table permission error, not an Always Encrypted one).
--
-- So CI needs CONTROL, which db_owner provides (it also supersedes DDL + read/write):
--   - db_owner — CREATE/ALTER/DROP, INSERT/UPDATE/DELETE, and CONTROL for the SYSTEM_VERSIONING toggle.
--
-- Tradeoff (accepted): db_owner implies SELECT, so this identity can read application data. The
-- earlier "no-read" posture (db_ddladmin + db_datawriter only) is therefore abandoned — it is
-- incompatible with automated migrations that alter temporal tables. PII columns protected by Always
-- Encrypted (Users.Email/DisplayName, Invitations.Email, *.Notes, ...) remain ciphertext to this
-- identity because it still holds no Key Vault Crypto access and so cannot decrypt them; non-encrypted
-- columns (names, addresses, contact values) are readable. The app's own managed identity gets its
-- data access via post-deploy.sql; that is where application reads/writes happen.
--
-- Prerequisites:
--   - Connect using Entra ID authentication as the SQL administrator.
--   - Replace <ci-identity-name> with the value from the 'ciIdentityName' Bicep output.
--
-- Example connection (Azure CLI):
--   sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault -i infrastructure/ci-grant.sql

-- Add the CI identity as a database user backed by its Entra ID service principal.
CREATE USER [<ci-identity-name>] FROM EXTERNAL PROVIDER;

-- db_owner: DDL + write for seed/backfill DML, plus CONTROL to toggle SYSTEM_VERSIONING when
-- migrations alter the system-versioned temporal tables. The idempotent EF script self-creates
-- [dbo].[__EFMigrationsHistory] on a fresh DB and reads it to find applied migrations; db_owner
-- covers that read, so no separate history-table pre-create or GRANT SELECT is needed.
ALTER ROLE db_owner ADD MEMBER [<ci-identity-name>];

-- NOTE: Always Encrypted setup (CMK/CEK creation, column encryption, temporal retention) is still NOT
-- performed by this identity. It requires Key Vault Crypto access to wrap the CEK, and is run through
-- the Gatherstead.Data.Setup tool as a one-off by a SQL admin (see docs/DEPLOYMENT.md), so the CI
-- identity needs no key-management grants here.

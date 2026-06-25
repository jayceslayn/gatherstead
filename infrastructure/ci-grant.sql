-- CI/CD grant script: give the GitHub Actions CI managed identity DDL access to the database.
-- Run this against the 'gatherstead' database once, after Bicep deployment provisions the CI identity.
--
-- The CI identity (gat-ci-id-*) applies EF Core migrations from build-and-test.yml. It needs schema
-- (DDL) rights only — never db_datareader/db_datawriter — so CI can change the schema but not read or
-- write application data. (The app's own managed identity gets data access via post-deploy.sql.)
--
-- Prerequisites:
--   - Connect using Entra ID authentication as the SQL administrator.
--   - Replace <ci-identity-name> with the value from the 'ciIdentityName' Bicep output.
--
-- Example connection (Azure CLI):
--   sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault -i infrastructure/ci-grant.sql

-- Add the CI identity as a database user backed by its Entra ID service principal.
CREATE USER [<ci-identity-name>] FROM EXTERNAL PROVIDER;

-- Grant DDL access required by EF Core migrations.
ALTER ROLE db_ddladmin ADD MEMBER [<ci-identity-name>];

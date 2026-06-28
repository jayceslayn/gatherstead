-- Post-deployment script: grant the API APP's managed identity access to the database.
-- Run this against the 'gatherstead' database after Bicep deployment completes.
--
-- This targets the API app identity ONLY (id-<workload>-<env>-<region>, e.g. id-gat-prod-wus2),
-- which gets read/write DML only. It is NOT the CI identity: the CI/migrations identity
-- (id-<workload>-ci-<env>-<region>, e.g. id-gat-ci-prod-wus2) gets db_ddladmin + db_datawriter
-- via the separate ci-grant.sql. Run both scripts; they grant different identities and do not
-- overlap.
--
-- Prerequisites:
--   - Connect using Entra ID authentication as the SQL administrator.
--   - Replace <managed-identity-name> with the value from the 'managedIdentityName' Bicep output.
--
-- Example connection (Azure CLI):
--   sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault

-- Add the API app's managed identity as a database user backed by its Entra ID service principal.
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'<managed-identity-name>')
    CREATE USER [<managed-identity-name>] FROM EXTERNAL PROVIDER;

-- Least privilege: the app only performs runtime DML. Schema changes (EF Core migrations) are
-- applied by the CI identity (see ci-grant.sql), so the app deliberately gets NO db_ddladmin.
-- ALTER ROLE ... ADD MEMBER is a no-op if the member already belongs, so this is re-run-safe.
ALTER ROLE db_datareader ADD MEMBER [<managed-identity-name>];
ALTER ROLE db_datawriter ADD MEMBER [<managed-identity-name>];

-- Post-deployment script: grant the app's managed identity access to the database.
-- Run this against the 'gatherstead' database after Bicep deployment completes.
--
-- Prerequisites:
--   - Connect using Entra ID authentication as the SQL administrator.
--   - Replace <managed-identity-name> with the value from the 'managedIdentityName' Bicep output.
--
-- Example connection (Azure CLI):
--   sqlcmd -S <sql-server-fqdn> -d gatherstead --authentication-method ActiveDirectoryDefault

-- Add the managed identity as a database user backed by its Entra ID service principal.
CREATE USER [<managed-identity-name>] FROM EXTERNAL PROVIDER;

-- Grant data access roles required by the application.
ALTER ROLE db_datareader ADD MEMBER [<managed-identity-name>];
ALTER ROLE db_datawriter ADD MEMBER [<managed-identity-name>];

-- Grant DDL access required by EF Core migrations.
ALTER ROLE db_ddladmin ADD MEMBER [<managed-identity-name>];

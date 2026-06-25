using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Azure.Identity;

namespace Gatherstead.Data.Setup;

class Program
{
    // Define the standard names for the keys.
    private const string CmkName = "CMK_Default";
    private const string CekName = "CEK_Default";

    static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: Gatherstead.Db.Setup <ConnectionString> <KeyVaultUrl>");
            return 1;
        }

        string connectionString = args[0];
        string keyVaultUrl = args[1];

        try
        {
            // Register the Azure Key Vault provider with the SQL client driver.
            // This allows the driver to interact with Azure Key Vault for cryptographic operations.
            // It uses DefaultAzureCredential for authentication, which will automatically use the
            // environment's managed identity, local Visual Studio/CLI credentials, etc.
            var provider = new SqlColumnEncryptionAzureKeyVaultProvider(new DefaultAzureCredential());
            SqlConnection.RegisterColumnEncryptionKeyStoreProviders(new Dictionary<string, SqlColumnEncryptionKeyStoreProvider> {
                { "AZURE_KEY_VAULT", provider }
            });

            using var connection = new SqlConnection(connectionString);
            connection.Open();
            Console.WriteLine("Successfully connected to the database.");

            EnsureColumnMasterKey(connection, keyVaultUrl);
            EnsureColumnEncryptionKey(connection);
            EnsureColumnEncryption(connection);
            EnsureTemporalRetention(connection);

            Console.WriteLine("\nDatabase setup completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void EnsureColumnMasterKey(SqlConnection connection, string keyVaultUrl)
    {
        Console.WriteLine($"\nChecking for Column Master Key '{CmkName}'...");

        var checkCmd = new SqlCommand($"SELECT COUNT(*) FROM sys.column_master_keys WHERE name = @name", connection);
        checkCmd.Parameters.AddWithValue("@name", CmkName);

        if ((int)checkCmd.ExecuteScalar() > 0)
        {
            Console.WriteLine($"Column Master Key '{CmkName}' already exists.");
            return;
        }

        Console.WriteLine($"Creating Column Master Key '{CmkName}'...");
        string sanitizedUrl = keyVaultUrl.Replace("'", "''");
        string createCmkSql = $@"
            CREATE COLUMN MASTER KEY [{CmkName}]
            WITH (
                KEY_STORE_PROVIDER_NAME = 'AZURE_KEY_VAULT',
                KEY_PATH = '{sanitizedUrl}'
            );";

        var createCmd = new SqlCommand(createCmkSql, connection);
        createCmd.ExecuteNonQuery();

        Console.WriteLine("Column Master Key created successfully.");
    }

    private static void EnsureColumnEncryptionKey(SqlConnection connection)
    {
        Console.WriteLine($"\nChecking for Column Encryption Key '{CekName}'...");

        var checkCmd = new SqlCommand($"SELECT COUNT(*) FROM sys.column_encryption_keys WHERE name = @name", connection);
        checkCmd.Parameters.AddWithValue("@name", CekName);

        if ((int)checkCmd.ExecuteScalar() > 0)
        {
            Console.WriteLine($"Column Encryption Key '{CekName}' already exists.");
            return;
        }

        Console.WriteLine($"Creating Column Encryption Key '{CekName}'...");
        string createCekSql = $@"
            CREATE COLUMN ENCRYPTION KEY [{CekName}]
            WITH VALUES (
                COLUMN_MASTER_KEY = [{CmkName}],
                ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256',
                ENCRYPTED_VALUE = 0x01  -- The driver replaces this with the actual encrypted value
            );";

        var createCmd = new SqlCommand(createCekSql, connection);
        createCmd.ExecuteNonQuery();

        Console.WriteLine("Column Encryption Key created successfully.");
    }

    private static void EnsureColumnEncryption(SqlConnection connection)
    {
        Console.WriteLine("\nChecking column-level encryption...");

        // Every column protected by Always Encrypted, as (table, column, sqlType, nullability, encType).
        // This is the single source of truth for column encryption (the old infrastructure/encrypt-columns.sql
        // was retired in favour of this idempotent utility). Nullability is explicit because ALTER COLUMN
        // defaults an unqualified column to NULL — omitting it would silently drop NOT NULL constraints.
        //
        // ContactMethods.Value uses Deterministic so equality lookups remain possible.
        // All other PII columns use Randomized (no filtering needed against those values).
        var columns = new[]
        {
            // HouseholdMember PII
            ("HouseholdMembers", "Name",         "NVARCHAR(200)", "NOT NULL", "RANDOMIZED"),
            ("HouseholdMembers", "BirthDate",     "DATE",          "NULL",     "RANDOMIZED"),
            ("HouseholdMembers", "DietaryNotes",  "NVARCHAR(500)", "NULL",     "RANDOMIZED"),
            ("HouseholdMembers", "Notes",         "NVARCHAR(500)", "NULL",     "RANDOMIZED"),
            ("ContactMethods",   "Value",         "NVARCHAR(256)", "NOT NULL", "DETERMINISTIC"),

            // Address PII
            ("Addresses",        "Line1",         "NVARCHAR(200)", "NOT NULL", "RANDOMIZED"),
            ("Addresses",        "Line2",         "NVARCHAR(200)", "NULL",     "RANDOMIZED"),
            ("Addresses",        "City",          "NVARCHAR(100)", "NOT NULL", "RANDOMIZED"),
            ("Addresses",        "State",         "NVARCHAR(100)", "NOT NULL", "RANDOMIZED"),
            ("Addresses",        "PostalCode",    "NVARCHAR(20)",  "NOT NULL", "RANDOMIZED"),
            ("Addresses",        "Country",       "NVARCHAR(100)", "NOT NULL", "RANDOMIZED"),

            // Top-level entity free-text Notes
            ("Tenants",              "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("Properties",           "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("Households",           "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("Events",               "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("Equipment",            "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("MealPlans",            "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("AccommodationIntents", "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("TaskTemplates",        "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("TaskPlans",            "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("MealAttendances",      "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("EventAttendances",     "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),
            ("MemberRelationships",  "Notes", "NVARCHAR(500)", "NULL", "RANDOMIZED"),

            // Attribute values (free-text user input)
            ("HouseholdMemberAttributes", "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("TenantAttributes",          "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("PropertyAttributes",        "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("AccommodationAttributes",   "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("HouseholdAttributes",       "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("EventAttributes",           "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("MealTemplateAttributes",    "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("TaskTemplateAttributes",    "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
            ("EquipmentAttributes",       "Value", "NVARCHAR(255)", "NOT NULL", "RANDOMIZED"),
        };

        foreach (var (table, column, sqlType, nullability, encType) in columns)
        {
            // sys.columns.encryption_type is non-NULL when the column is already encrypted.
            var checkCmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM sys.columns c
                JOIN sys.tables  t ON c.object_id = t.object_id
                WHERE t.name = @table AND c.name = @column
                  AND c.encryption_type IS NOT NULL", connection);
            checkCmd.Parameters.AddWithValue("@table", table);
            checkCmd.Parameters.AddWithValue("@column", column);

            if ((int)checkCmd.ExecuteScalar() > 0)
            {
                Console.WriteLine($"  {table}.{column} is already encrypted — skipping.");
                continue;
            }

            Console.WriteLine($"  Encrypting {table}.{column} ({encType})...");

            // Always Encrypted with Secure Enclaves supports in-place encryption via ALTER COLUMN.
            // String columns require a BIN2 collation; non-string types (DATE) do not.
            var collation = sqlType.StartsWith("NVARCHAR", StringComparison.OrdinalIgnoreCase)
                ? " COLLATE Latin1_General_BIN2"
                : string.Empty;

            // Clause order is significant for in-place ALTER COLUMN encryption: nullability must
            // follow the ENCRYPTED WITH (...) clause (and precede WITH (ONLINE = ON)). Placing it
            // before ENCRYPTED — as the generic column_definition grammar allows — is rejected here
            // with "Incorrect syntax near 'ENCRYPTED'".
            var sql = $"""
                ALTER TABLE dbo.[{table}]
                ALTER COLUMN [{column}] {sqlType}{collation}
                ENCRYPTED WITH (
                    COLUMN_ENCRYPTION_KEY = [{CekName}],
                    ENCRYPTION_TYPE = {encType},
                    ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
                ) {nullability} WITH (ONLINE = ON);
                """;

            var alterCmd = new SqlCommand(sql, connection);
            alterCmd.ExecuteNonQuery();
            Console.WriteLine($"  {table}.{column} encrypted successfully.");
        }

        Console.WriteLine("Column encryption check complete.");
    }

    private static void EnsureTemporalRetention(SqlConnection connection)
    {
        Console.WriteLine("\nConfiguring temporal history retention...");

        // Enable retention policy at the database level (idempotent).
        var enableCmd = new SqlCommand(
            "ALTER DATABASE CURRENT SET TEMPORAL_HISTORY_RETENTION ON;", connection);
        enableCmd.ExecuteNonQuery();
        Console.WriteLine("Temporal history retention enabled on database.");

        // Find all temporal tables and set a 1-year retention period.
        var findCmd = new SqlCommand(@"
            SELECT QUOTENAME(SCHEMA_NAME(schema_id)) AS SchemaName, QUOTENAME(name) AS TableName
            FROM sys.tables
            WHERE temporal_type = 2;", connection);

        var tables = new List<(string Schema, string Table)>();
        using (var reader = findCmd.ExecuteReader())
        {
            while (reader.Read())
                tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        foreach (var (schema, table) in tables)
        {
            var alterCmd = new SqlCommand($@"
                ALTER TABLE {schema}.{table}
                SET (SYSTEM_VERSIONING = ON (
                    HISTORY_RETENTION_PERIOD = 1 YEAR));", connection);
            alterCmd.ExecuteNonQuery();
            Console.WriteLine($"  Set 1-year retention on {schema}.{table}.");
        }

        Console.WriteLine($"Temporal retention configured for {tables.Count} table(s).");
    }
}

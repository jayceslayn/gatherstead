using System.Security.Cryptography;
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

            EnsureColumnMasterKey(connection, provider, keyVaultUrl);
            EnsureColumnEncryptionKey(connection, provider, keyVaultUrl);
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

    private static void EnsureColumnMasterKey(
        SqlConnection connection, SqlColumnEncryptionAzureKeyVaultProvider keyStoreProvider, string keyVaultUrl)
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

        // The CMK must be enclave-enabled, because columns are encrypted in place via ALTER COLUMN —
        // an Always Encrypted *with secure enclaves* operation that the server refuses unless the key
        // permits enclave computations. The signature attests, under the CMK's private key in Key Vault,
        // that this key path is approved for enclave use; the server verifies it before the enclave may
        // use the key. SignColumnMasterKeyMetadata calls Key Vault's sign operation (RS256), so the
        // Key Vault key needs the 'sign' permission. The hex is our own generated value, not user input.
        byte[] signature = keyStoreProvider.SignColumnMasterKeyMetadata(keyVaultUrl, allowEnclaveComputations: true);
        string signatureHex = "0x" + Convert.ToHexString(signature);

        string createCmkSql = $@"
            CREATE COLUMN MASTER KEY [{CmkName}]
            WITH (
                KEY_STORE_PROVIDER_NAME = 'AZURE_KEY_VAULT',
                KEY_PATH = '{sanitizedUrl}',
                ENCLAVE_COMPUTATIONS (SIGNATURE = {signatureHex})
            );";

        var createCmd = new SqlCommand(createCmkSql, connection);
        createCmd.ExecuteNonQuery();

        Console.WriteLine("Column Master Key created successfully (enclave-enabled).");
    }

    private static void EnsureColumnEncryptionKey(
        SqlConnection connection, SqlColumnEncryptionAzureKeyVaultProvider keyStoreProvider, string keyVaultUrl)
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

        // Raw T-SQL CREATE COLUMN ENCRYPTION KEY does NOT generate or wrap a key — unlike SSMS/PowerShell,
        // it stores whatever ENCRYPTED_VALUE is supplied verbatim. So we must generate the CEK and wrap it
        // ourselves: a random 256-bit key, encrypted with the CMK in Key Vault via RSA_OAEP (the CMK
        // key-wrap algorithm — distinct from the AEAD algorithm used for the column data itself).
        byte[] plaintextCek = RandomNumberGenerator.GetBytes(32);
        byte[] encryptedCek = keyStoreProvider.EncryptColumnEncryptionKey(keyVaultUrl, "RSA_OAEP", plaintextCek);
        string encryptedCekHex = "0x" + Convert.ToHexString(encryptedCek);

        // ENCRYPTED_VALUE is a binary literal and can't be parameterized; the hex is our own generated
        // value (not user input), so interpolating it is safe.
        string createCekSql = $@"
            CREATE COLUMN ENCRYPTION KEY [{CekName}]
            WITH VALUES (
                COLUMN_MASTER_KEY = [{CmkName}],
                ALGORITHM = 'RSA_OAEP',
                ENCRYPTED_VALUE = {encryptedCekHex}
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
        // Deterministic encryption (vs Randomized) is required for any column that must support
        // server-side equality — including being part of an index. HouseholdMembers.Name backs the
        // (TenantId, HouseholdId, Name) index, and ContactMethods.Value needs equality lookups, so
        // both are Deterministic. All other PII columns use Randomized (never compared server-side).
        var columns = new[]
        {
            // HouseholdMember PII
            ("HouseholdMembers", "Name",         "NVARCHAR(200)", "NOT NULL", "DETERMINISTIC"),
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

            // The column definition fragment that follows "ALTER COLUMN [name]". Clause order is
            // significant: nullability must follow the ENCRYPTED WITH (...) clause (placing it before
            // ENCRYPTED — as the generic column_definition grammar allows — is rejected with
            // "Incorrect syntax near 'ENCRYPTED'"). Combining a collation change with encryption in
            // one statement is permitted.
            var encryptedColumnDef = $"""
                {sqlType}{collation}
                ENCRYPTED WITH (
                    COLUMN_ENCRYPTION_KEY = [{CekName}],
                    ENCRYPTION_TYPE = {encType},
                    ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
                ) {nullability}
                """;

            // If the table is system-versioned (temporal), the history table is its pair.
            var historyCmd = new SqlCommand(@"
                SELECT SCHEMA_NAME(h.schema_id), h.name
                FROM sys.tables t
                JOIN sys.tables h ON t.history_table_id = h.object_id
                WHERE t.name = @table AND SCHEMA_NAME(t.schema_id) = 'dbo' AND t.temporal_type = 2", connection);
            historyCmd.Parameters.AddWithValue("@table", table);

            string? historySchema = null, historyTable = null;
            using (var reader = historyCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    historySchema = reader.GetString(0);
                    historyTable = reader.GetString(1);
                }
            }

            // An index referencing the column blocks ALTER COLUMN, so drop any such index before
            // encrypting and recreate it afterwards. Recreating only works because indexed PII columns
            // are Deterministic (an index cannot reference a Randomized column) — see the column list.
            var (dropIndexes, createIndexes) = ScriptDependentIndexes(connection, table, column);

            var statements = new List<string>();
            if (historyTable is not null)
            {
                // Temporal tables reject online ALTER COLUMN, and in-place encryption is not
                // propagated to the history table automatically. Disable versioning, encrypt the
                // column on both the current and history tables (their schemas must match to
                // re-enable versioning), then re-enable. Wrapped in a transaction with XACT_ABORT so
                // any failure rolls the whole thing back and restores versioning rather than leaving
                // the table unversioned; DATA_CONSISTENCY_CHECK is safe to skip inside the
                // transaction since no concurrent writes can occur. This keeps every statement at
                // ALTER permission (db_ddladmin) — directly altering a still-versioned table would
                // instead require CONTROL, which implies SELECT and would break the CI no-read rule.
                // Secondary indexes exist only on the current table, so they are dropped/recreated there.
                statements.Add("SET XACT_ABORT ON;");
                statements.Add("BEGIN TRANSACTION;");
                statements.Add($"ALTER TABLE dbo.[{table}] SET (SYSTEM_VERSIONING = OFF);");
                statements.AddRange(dropIndexes);
                statements.Add($"ALTER TABLE dbo.[{table}] ALTER COLUMN [{column}] {encryptedColumnDef};");
                statements.Add($"ALTER TABLE [{historySchema}].[{historyTable}] ALTER COLUMN [{column}] {encryptedColumnDef};");
                statements.AddRange(createIndexes);
                statements.Add($"ALTER TABLE dbo.[{table}] SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [{historySchema}].[{historyTable}], DATA_CONSISTENCY_CHECK = OFF));");
                statements.Add("COMMIT TRANSACTION;");
            }
            else
            {
                // Non-temporal table: encrypt in place online.
                statements.AddRange(dropIndexes);
                statements.Add($"ALTER TABLE dbo.[{table}] ALTER COLUMN [{column}] {encryptedColumnDef} WITH (ONLINE = ON);");
                statements.AddRange(createIndexes);
            }

            // In-place encryption time scales with row count; don't let a long-running ALTER time out.
            var alterCmd = new SqlCommand(string.Join("\n", statements), connection) { CommandTimeout = 0 };
            alterCmd.ExecuteNonQuery();
            Console.WriteLine($"  {table}.{column} encrypted successfully.");
        }

        Console.WriteLine("Column encryption check complete.");
    }

    // Returns the DROP INDEX / CREATE INDEX statements for every secondary (nonclustered) index on
    // dbo.[table] that references [column]. Such an index blocks ALTER COLUMN, so it must be dropped
    // before encrypting and recreated after. Definitions are reconstructed from the catalog views so
    // the recreated index matches what EF created (key/included columns, order, uniqueness, filter).
    private static (List<string> Drops, List<string> Creates) ScriptDependentIndexes(
        SqlConnection connection, string table, string column)
    {
        var cmd = new SqlCommand("""
            SELECT i.name AS IndexName, i.is_unique, i.is_primary_key, i.is_unique_constraint,
                   i.filter_definition, c.name AS ColumnName, ic.is_included_column,
                   ic.key_ordinal, ic.is_descending_key
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE i.object_id = OBJECT_ID(@qualified)
              AND i.index_id > 1  -- nonclustered secondary indexes only (skip heap/clustered)
              AND i.index_id IN (
                  SELECT ic2.index_id
                  FROM sys.index_columns ic2
                  JOIN sys.columns c2 ON ic2.object_id = c2.object_id AND ic2.column_id = c2.column_id
                  WHERE ic2.object_id = OBJECT_ID(@qualified) AND c2.name = @column)
            ORDER BY i.name, ic.is_included_column, ic.key_ordinal;
            """, connection);
        cmd.Parameters.AddWithValue("@qualified", $"dbo.{table}");
        cmd.Parameters.AddWithValue("@column", column);

        // Group the column rows by index, preserving key/included column order from the query.
        var indexes = new Dictionary<string, (bool IsUnique, string? Filter, List<string> Keys, List<string> Included)>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(0);
                if (reader.GetBoolean(2) || reader.GetBoolean(3))
                    throw new NotSupportedException(
                        $"Index '{name}' on {table}.{column} is a primary key or unique constraint; " +
                        "encrypting a column it covers is not supported by this tool.");

                if (!indexes.TryGetValue(name, out var entry))
                {
                    entry = (reader.GetBoolean(1), reader.IsDBNull(4) ? null : reader.GetString(4),
                             new List<string>(), new List<string>());
                    indexes[name] = entry;
                }

                var colName = reader.GetString(5);
                if (reader.GetBoolean(6))
                    entry.Included.Add($"[{colName}]");
                else
                    entry.Keys.Add($"[{colName}]" + (reader.GetBoolean(8) ? " DESC" : string.Empty));
            }
        }

        var drops = new List<string>();
        var creates = new List<string>();
        foreach (var (name, def) in indexes)
        {
            drops.Add($"DROP INDEX [{name}] ON dbo.[{table}];");

            var unique = def.IsUnique ? "UNIQUE " : string.Empty;
            var include = def.Included.Count > 0 ? $" INCLUDE ({string.Join(", ", def.Included)})" : string.Empty;
            var filter = def.Filter is not null ? $" WHERE {def.Filter}" : string.Empty;
            creates.Add($"CREATE {unique}INDEX [{name}] ON dbo.[{table}] ({string.Join(", ", def.Keys)}){include}{filter};");
        }
        return (drops, creates);
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

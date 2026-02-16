using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;
using Azure.Identity;

namespace Gatherstead.Data.Setup;

class Program
{
    // Define the standard names for the keys.
    private const string CmkName = "CMK_Default";
    private const string CekName = "CEK_Default";

    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: Gatherstead.Db.Setup <ConnectionString> <KeyVaultUrl>");
            return;
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

            Console.WriteLine("\nDatabase encryption keys configured successfully.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.ResetColor();
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
        string createCmkSql = $@"
            CREATE COLUMN MASTER KEY [{CmkName}]
            WITH (
                KEY_STORE_PROVIDER_NAME = 'AZURE_KEY_VAULT',
                KEY_PATH = '{keyVaultUrl}'
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
}

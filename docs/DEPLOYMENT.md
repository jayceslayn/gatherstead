# Database Encryption and Deployment
This project uses Always Encrypted with Secure Enclaves to protect sensitive data. The setup and deployment are managed through an automated, Infrastructure as Code (IaC) workflow.

## Automated Deployment Workflow
The end-to-end process is designed to be run from a CI/CD pipeline:

1.  **Provision Infrastructure:** Run the Terraform script in the `infrastructure/` directory. This will provision all necessary Azure resources, including the Key Vault, the master key, the SQL server, and the database.
2.  **Run EF Core Migrations:** The application's deployment pipeline should run standard Entity Framework Core migrations to create the database schema.
3.  **Configure Database Keys:** Execute the `Gatherstead.Db.Setup` utility, passing in the database connection string and the Key Vault CMK URI (both are outputs from the Terraform script). This utility connects to the database and creates the necessary Column Master Key and Column Encryption Key metadata.
4.  **Encrypt Columns:** Execute the `infrastructure/encrypt-columns.sql` script against the database. This script alters the table columns to apply encryption.
5.  **Deploy Application:** Deploy the application itself.

## Manual Setup Steps

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

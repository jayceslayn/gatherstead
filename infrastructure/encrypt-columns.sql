-- This script applies Always Encrypted to the necessary columns.
-- It should be run after the Column Master Key and Column Encryption Key have been created in the database.

-- HouseholdMember table
ALTER TABLE dbo.HouseholdMembers
    ALTER COLUMN Name NVARCHAR(200) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Deterministic,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.HouseholdMembers
    ALTER COLUMN BirthDate DATE NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.HouseholdMembers
    ALTER COLUMN DietaryNotes NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

-- Other tables with encrypted Notes
ALTER TABLE dbo.Equipment
    ALTER COLUMN Notes NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.MealPlans
    ALTER COLUMN Notes NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.MealIntents
    ALTER COLUMN Notes NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.AccommodationIntents
    ALTER COLUMN Notes NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.TaskTemplates
    ALTER COLUMN Notes NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.TaskPlans
    ALTER COLUMN Notes NVARCHAR(MAX) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

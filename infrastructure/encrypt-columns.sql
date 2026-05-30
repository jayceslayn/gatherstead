-- This script applies Always Encrypted to the necessary columns.
-- It should be run after the Column Master Key and Column Encryption Key have been created in the database.
-- All NVARCHAR columns use NVARCHAR(n) with a fixed bound — Always Encrypted does not support NVARCHAR(MAX).

-- ============================================================
-- HouseholdMembers: PII fields
-- ============================================================
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
    ALTER COLUMN DietaryNotes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.HouseholdMembers
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

-- ============================================================
-- Top-level entity Notes fields (all free-text, all Randomized)
-- ============================================================
ALTER TABLE dbo.Tenants
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.Properties
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.Households
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.Events
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.Equipment
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

-- ============================================================
-- Planning entity Notes fields
-- ============================================================
ALTER TABLE dbo.MealPlans
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.AccommodationIntents
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.TaskTemplates
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.TaskPlans
    ALTER COLUMN Notes NVARCHAR(500) COLLATE Latin1_General_BIN2 NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

-- ============================================================
-- Attribute Values (all free-text user input, all Randomized)
-- ============================================================
ALTER TABLE dbo.HouseholdMemberAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.TenantAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.PropertyAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.AccommodationAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.HouseholdAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.EventAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.MealTemplateAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.TaskTemplateAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

ALTER TABLE dbo.EquipmentAttributes
    ALTER COLUMN Value NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL
    ENCRYPTED WITH (
        COLUMN_ENCRYPTION_KEY = [CEK_Default],
        ENCRYPTION_TYPE = Randomized,
        ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256'
    );
GO

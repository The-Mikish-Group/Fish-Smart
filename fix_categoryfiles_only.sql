-- Fix CategoryFiles table to match Oaks-Village working structure
-- Run this script against your Fish-Smart database

USE [YOUR_DATABASE_NAME] -- Replace with your actual database name
GO

PRINT 'Fixing CategoryFiles table to match Oaks-Village structure...'
PRINT '============================================================='

-- Check if CategoryFiles table exists
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CategoryFiles')
BEGIN
    PRINT 'CategoryFiles table does not exist. Creating with correct structure...'
    
    CREATE TABLE [dbo].[CategoryFiles] (
        [FileID]     INT            IDENTITY (1, 1) NOT NULL,
        [CategoryID] INT            NOT NULL,
        [FileName]   NVARCHAR (255) NOT NULL,
        [SortOrder]  INT            NOT NULL,
        CONSTRAINT [PK_CategoryFiles] PRIMARY KEY CLUSTERED ([FileID] ASC)
    );
    
    PRINT 'CategoryFiles table created successfully.'
    GOTO CreateIndexAndFK
END

PRINT 'CategoryFiles table exists. Checking if structure matches Oaks-Village...'

-- Check table structure
DECLARE @NeedsUpdate BIT = 0
DECLARE @Issues NVARCHAR(MAX) = ''

-- Check FileID column (should be INT IDENTITY, primary key)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CategoryFiles' 
    AND COLUMN_NAME = 'FileID' 
    AND DATA_TYPE = 'int' 
    AND IS_NULLABLE = 'NO'
) OR COLUMNPROPERTY(OBJECT_ID('CategoryFiles'), 'FileID', 'IsIdentity') = 0
BEGIN
    SET @NeedsUpdate = 1
    SET @Issues = @Issues + 'FileID not properly configured as INT IDENTITY; '
END

-- Check CategoryID column (should be INT NOT NULL)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CategoryFiles' 
    AND COLUMN_NAME = 'CategoryID' 
    AND DATA_TYPE = 'int' 
    AND IS_NULLABLE = 'NO'
)
BEGIN
    SET @NeedsUpdate = 1
    SET @Issues = @Issues + 'CategoryID not INT NOT NULL; '
END

-- Check FileName column (should be NVARCHAR(255) NOT NULL)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CategoryFiles' 
    AND COLUMN_NAME = 'FileName' 
    AND DATA_TYPE = 'nvarchar' 
    AND CHARACTER_MAXIMUM_LENGTH = 255
    AND IS_NULLABLE = 'NO'
)
BEGIN
    SET @NeedsUpdate = 1
    SET @Issues = @Issues + 'FileName not NVARCHAR(255) NOT NULL; '
END

-- Check SortOrder column (should be INT NOT NULL)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'CategoryFiles' 
    AND COLUMN_NAME = 'SortOrder' 
    AND DATA_TYPE = 'int' 
    AND IS_NULLABLE = 'NO'
)
BEGIN
    SET @NeedsUpdate = 1
    SET @Issues = @Issues + 'SortOrder not INT NOT NULL; '
END

IF @NeedsUpdate = 1
BEGIN
    PRINT 'CategoryFiles table structure issues found: ' + @Issues
    PRINT 'Recreating CategoryFiles table with correct Oaks-Village structure...'
    
    -- Backup existing data if any
    IF EXISTS (SELECT 1 FROM CategoryFiles)
    BEGIN
        SELECT * INTO CategoryFiles_Backup FROM CategoryFiles
        PRINT 'Existing data backed up to CategoryFiles_Backup'
    END
    
    -- Drop existing constraints and indexes
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_CategoryFiles_PDFCategories' 
               AND TABLE_NAME = 'CategoryFiles')
    BEGIN
        ALTER TABLE [dbo].[CategoryFiles] DROP CONSTRAINT [FK_CategoryFiles_PDFCategories]
        PRINT 'Dropped FK_CategoryFiles_PDFCategories constraint'
    END
    
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CategoryFiles_CategoryID' AND object_id = OBJECT_ID('CategoryFiles'))
    BEGIN
        DROP INDEX [IX_CategoryFiles_CategoryID] ON [dbo].[CategoryFiles]
        PRINT 'Dropped IX_CategoryFiles_CategoryID index'
    END
    
    -- Drop the old table
    DROP TABLE [dbo].[CategoryFiles]
    PRINT 'Dropped old CategoryFiles table'
    
    -- Create new table with exact Oaks-Village structure
    CREATE TABLE [dbo].[CategoryFiles] (
        [FileID]     INT            IDENTITY (1, 1) NOT NULL,
        [CategoryID] INT            NOT NULL,
        [FileName]   NVARCHAR (255) NOT NULL,
        [SortOrder]  INT            NOT NULL,
        CONSTRAINT [PK_CategoryFiles] PRIMARY KEY CLUSTERED ([FileID] ASC)
    );
    PRINT 'Created CategoryFiles table with exact Oaks-Village structure'
    
    -- Restore data if backup exists
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CategoryFiles_Backup')
    BEGIN
        SET IDENTITY_INSERT [dbo].[CategoryFiles] OFF
        
        INSERT INTO [dbo].[CategoryFiles] ([CategoryID], [FileName], [SortOrder])
        SELECT 
            [CategoryID],
            CAST([FileName] AS NVARCHAR(255)),
            [SortOrder]
        FROM CategoryFiles_Backup
        WHERE [CategoryID] IS NOT NULL 
          AND [FileName] IS NOT NULL 
          AND [SortOrder] IS NOT NULL
        ORDER BY 
            CASE WHEN COLUMNPROPERTY(OBJECT_ID('CategoryFiles_Backup'), 'FileID', 'IsIdentity') = 1 
                 THEN [FileID] 
                 ELSE [SortOrder] END
        
        PRINT 'Restored data from backup'
    END
END
ELSE
BEGIN
    PRINT 'CategoryFiles table structure already matches Oaks-Village'
END

CreateIndexAndFK:
-- Create the non-clustered index (matches Oaks-Village exactly)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CategoryFiles_CategoryID' AND object_id = OBJECT_ID('CategoryFiles'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CategoryFiles_CategoryID]
        ON [dbo].[CategoryFiles]([CategoryID] ASC);
    PRINT 'Created IX_CategoryFiles_CategoryID index'
END
ELSE
BEGIN
    PRINT 'IX_CategoryFiles_CategoryID index already exists'
END

-- Create foreign key constraint (matches Oaks-Village exactly)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_CategoryFiles_PDFCategories' 
               AND TABLE_NAME = 'CategoryFiles')
BEGIN
    ALTER TABLE [dbo].[CategoryFiles]
        ADD CONSTRAINT [FK_CategoryFiles_PDFCategories] 
        FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[PDFCategories] ([CategoryID]) ON DELETE CASCADE;
    PRINT 'Created FK_CategoryFiles_PDFCategories foreign key constraint'
END
ELSE
BEGIN
    PRINT 'FK_CategoryFiles_PDFCategories foreign key constraint already exists'
END

-- Final verification
PRINT ''
PRINT 'Final CategoryFiles table structure:'
PRINT '===================================='
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT,
    CASE WHEN COLUMNPROPERTY(OBJECT_ID('CategoryFiles'), c.COLUMN_NAME, 'IsIdentity') = 1 
         THEN 'YES' ELSE 'NO' END as IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'CategoryFiles'
ORDER BY c.ORDINAL_POSITION;

PRINT ''
PRINT 'CategoryFiles table now matches Oaks-Village structure exactly!'

-- Uncomment the line below if you want to clean up the backup table
-- DROP TABLE CategoryFiles_Backup
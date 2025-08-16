-- Fix CategoryFiles table to match working Oaks-Village structure
-- Run this script against the Fish-Smart database

USE [YOUR_FISH_SMART_DATABASE_NAME]
GO

-- First, check if the table exists and examine its current structure
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CategoryFiles')
BEGIN
    PRINT 'CategoryFiles table exists. Checking structure...'
    
    -- Drop existing foreign key constraints if they exist
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_CategoryFiles_PDFCategories' 
               AND TABLE_NAME = 'CategoryFiles')
    BEGIN
        ALTER TABLE [dbo].[CategoryFiles] DROP CONSTRAINT [FK_CategoryFiles_PDFCategories]
        PRINT 'Dropped existing FK_CategoryFiles_PDFCategories constraint'
    END
    
    -- Drop existing indexes if they exist
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CategoryFiles_CategoryID' AND object_id = OBJECT_ID('CategoryFiles'))
    BEGIN
        DROP INDEX [IX_CategoryFiles_CategoryID] ON [dbo].[CategoryFiles]
        PRINT 'Dropped existing IX_CategoryFiles_CategoryID index'
    END
    
    -- Check if all required columns exist with correct data types
    DECLARE @MissingColumns NVARCHAR(MAX) = ''
    
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'CategoryFiles' AND COLUMN_NAME = 'FileID' 
                   AND DATA_TYPE = 'int' AND IS_NULLABLE = 'NO')
    BEGIN
        SET @MissingColumns = @MissingColumns + 'FileID (int, identity), '
    END
    
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'CategoryFiles' AND COLUMN_NAME = 'CategoryID' 
                   AND DATA_TYPE = 'int' AND IS_NULLABLE = 'NO')
    BEGIN
        SET @MissingColumns = @MissingColumns + 'CategoryID (int), '
    END
    
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'CategoryFiles' AND COLUMN_NAME = 'FileName' 
                   AND DATA_TYPE = 'nvarchar' AND CHARACTER_MAXIMUM_LENGTH = 255 AND IS_NULLABLE = 'NO')
    BEGIN
        SET @MissingColumns = @MissingColumns + 'FileName (nvarchar(255)), '
    END
    
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'CategoryFiles' AND COLUMN_NAME = 'SortOrder' 
                   AND DATA_TYPE = 'int' AND IS_NULLABLE = 'NO')
    BEGIN
        SET @MissingColumns = @MissingColumns + 'SortOrder (int), '
    END
    
    IF LEN(@MissingColumns) > 0
    BEGIN
        PRINT 'Table structure needs modification. Missing or incorrect columns: ' + @MissingColumns
        PRINT 'Backing up existing data...'
        
        -- Create backup table
        SELECT * INTO CategoryFiles_Backup FROM CategoryFiles
        PRINT 'Data backed up to CategoryFiles_Backup table'
        
        -- Drop and recreate table with correct structure
        DROP TABLE [dbo].[CategoryFiles]
        PRINT 'Dropped existing CategoryFiles table'
    END
    ELSE
    BEGIN
        PRINT 'Table structure appears correct. Adding constraints...'
        GOTO AddConstraints
    END
END

-- Create the CategoryFiles table with the correct structure
CREATE TABLE [dbo].[CategoryFiles] (
    [FileID]     INT            IDENTITY (1, 1) NOT NULL,
    [CategoryID] INT            NOT NULL,
    [FileName]   NVARCHAR (255) NOT NULL,
    [SortOrder]  INT            NOT NULL,
    CONSTRAINT [PK_CategoryFiles] PRIMARY KEY CLUSTERED ([FileID] ASC)
);
PRINT 'Created CategoryFiles table with correct structure'

-- Restore data if backup exists
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CategoryFiles_Backup')
BEGIN
    INSERT INTO [dbo].[CategoryFiles] ([CategoryID], [FileName], [SortOrder])
    SELECT [CategoryID], [FileName], [SortOrder] 
    FROM CategoryFiles_Backup
    WHERE [CategoryID] IS NOT NULL 
      AND [FileName] IS NOT NULL 
      AND [SortOrder] IS NOT NULL
    
    PRINT 'Restored data from backup table'
    
    -- Optionally drop the backup table (uncomment if you want to clean up)
    -- DROP TABLE CategoryFiles_Backup
    -- PRINT 'Dropped backup table'
END

AddConstraints:
-- Create the non-clustered index
CREATE NONCLUSTERED INDEX [IX_CategoryFiles_CategoryID]
    ON [dbo].[CategoryFiles]([CategoryID] ASC);
PRINT 'Created IX_CategoryFiles_CategoryID index'

-- Add the foreign key constraint
ALTER TABLE [dbo].[CategoryFiles]
    ADD CONSTRAINT [FK_CategoryFiles_PDFCategories] 
    FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[PDFCategories] ([CategoryID]) ON DELETE CASCADE;
PRINT 'Added FK_CategoryFiles_PDFCategories foreign key constraint'

PRINT 'CategoryFiles table structure updated successfully!'

-- Verify the final structure
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'CategoryFiles'
ORDER BY c.ORDINAL_POSITION;
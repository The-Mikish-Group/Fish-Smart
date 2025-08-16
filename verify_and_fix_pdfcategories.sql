-- Verify and fix PDFCategories table structure to match working system
-- Run this script on your database: db_a7b035_fishsmart

USE db_a7b035_fishsmart;
GO

-- Check current table structure
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT,
    COLUMNPROPERTY(OBJECT_ID('PDFCategories'), c.COLUMN_NAME, 'IsIdentity') as IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'PDFCategories'
ORDER BY c.ORDINAL_POSITION;

-- Check if the table structure matches the working system
DECLARE @NeedsUpdate BIT = 0;

-- Check if CategoryID is IDENTITY
IF COLUMNPROPERTY(OBJECT_ID('PDFCategories'), 'CategoryID', 'IsIdentity') = 0
BEGIN
    PRINT 'CategoryID is not IDENTITY - needs update';
    SET @NeedsUpdate = 1;
END

-- Check if CategoryName is NVARCHAR(255)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'PDFCategories' 
    AND COLUMN_NAME = 'CategoryName' 
    AND DATA_TYPE = 'nvarchar' 
    AND CHARACTER_MAXIMUM_LENGTH = 255
)
BEGIN
    PRINT 'CategoryName column type needs update';
    SET @NeedsUpdate = 1;
END

-- If updates are needed, recreate the table with correct structure
IF @NeedsUpdate = 1
BEGIN
    PRINT 'Updating PDFCategories table structure...';
    
    -- Check if there are any existing records
    DECLARE @HasData BIT = 0;
    IF EXISTS (SELECT 1 FROM PDFCategories)
        SET @HasData = 1;
    
    -- Create the new table with correct structure
    CREATE TABLE PDFCategories_New (
        [CategoryID]   INT            IDENTITY (1, 1) NOT NULL,
        [CategoryName] NVARCHAR (255) NOT NULL,
        [SortOrder]    INT            NOT NULL,
        [IsAdminOnly]  BIT            DEFAULT ((0)) NOT NULL,
        PRIMARY KEY CLUSTERED ([CategoryID] ASC)
    );
    
    -- If there's existing data, migrate it
    IF @HasData = 1
    BEGIN
        -- Create mapping table for foreign key updates
        CREATE TABLE #CategoryMapping (
            OldID INT,
            NewID INT,
            CategoryName NVARCHAR(255)
        );
        
        -- Copy data preserving order
        SET IDENTITY_INSERT PDFCategories_New OFF;
        INSERT INTO PDFCategories_New (CategoryName, SortOrder, IsAdminOnly)
        SELECT 
            CAST(CategoryName AS NVARCHAR(255)), 
            SortOrder, 
            ISNULL(IsAdminOnly, 0)
        FROM PDFCategories 
        ORDER BY CategoryID;
        
        -- Create mapping for foreign key updates
        INSERT INTO #CategoryMapping (OldID, NewID, CategoryName)
        SELECT 
            old.CategoryID,
            new.CategoryID,
            old.CategoryName
        FROM PDFCategories old
        INNER JOIN PDFCategories_New new ON old.CategoryName = new.CategoryName 
            AND old.SortOrder = new.SortOrder
            AND ISNULL(old.IsAdminOnly, 0) = new.IsAdminOnly;
        
        -- Update CategoryFiles foreign keys if the table exists
        IF OBJECT_ID('CategoryFiles', 'U') IS NOT NULL
        BEGIN
            UPDATE cf
            SET cf.CategoryID = cm.NewID
            FROM CategoryFiles cf
            INNER JOIN #CategoryMapping cm ON cf.CategoryID = cm.OldID;
            
            PRINT 'Updated CategoryFiles foreign key references';
        END
        
        DROP TABLE #CategoryMapping;
    END
    
    -- Replace the old table
    DROP TABLE PDFCategories;
    EXEC sp_rename 'PDFCategories_New', 'PDFCategories';
    
    PRINT 'PDFCategories table structure updated successfully';
END
ELSE
BEGIN
    PRINT 'PDFCategories table structure is already correct';
END

-- Final verification
PRINT 'Final table structure:';
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT,
    COLUMNPROPERTY(OBJECT_ID('PDFCategories'), c.COLUMN_NAME, 'IsIdentity') as IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'PDFCategories'
ORDER BY c.ORDINAL_POSITION;
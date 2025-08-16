-- Fix PDFCategories table to make CategoryID auto-increment (IDENTITY)
-- This is a simpler approach that preserves existing data
-- Run this script on your database: db_a7b035_fishsmart

USE db_a7b035_fishsmart;
GO

-- Check current table structure and data
SELECT 'Current CategoryID values:' as Info;
SELECT CategoryID, CategoryName, SortOrder, IsAdminOnly FROM PDFCategories ORDER BY CategoryID;

-- Step 1: Get the maximum CategoryID value for reseeding
DECLARE @MaxID INT;
SELECT @MaxID = ISNULL(MAX(CategoryID), 0) FROM PDFCategories;
PRINT 'Current maximum CategoryID: ' + CAST(@MaxID AS VARCHAR(10));

-- Step 2: Check if CategoryID is already an IDENTITY column
SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    COLUMNPROPERTY(OBJECT_ID('PDFCategories'), c.COLUMN_NAME, 'IsIdentity') as IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'PDFCategories' AND c.COLUMN_NAME = 'CategoryID';

-- If CategoryID is not already an IDENTITY column, we need to recreate it
-- Check if we need to fix it
IF COLUMNPROPERTY(OBJECT_ID('PDFCategories'), 'CategoryID', 'IsIdentity') = 0
BEGIN
    PRINT 'CategoryID is not an IDENTITY column. Fixing...';
    
    -- Step 3: Create a temporary table with the correct structure
    CREATE TABLE PDFCategories_Temp (
        CategoryID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(MAX) NOT NULL,
        SortOrder INT NOT NULL,
        IsAdminOnly BIT NOT NULL DEFAULT 0
    );
    
    -- Step 4: Copy data to temp table (IDENTITY will auto-assign new IDs)
    SET IDENTITY_INSERT PDFCategories_Temp OFF;
    INSERT INTO PDFCategories_Temp (CategoryName, SortOrder, IsAdminOnly)
    SELECT CategoryName, SortOrder, IsAdminOnly 
    FROM PDFCategories 
    ORDER BY CategoryID;
    
    -- Step 5: Update CategoryFiles to reference new IDs
    -- First, create a mapping table
    CREATE TABLE #CategoryMapping (
        OldID INT,
        NewID INT,
        CategoryName NVARCHAR(MAX)
    );
    
    -- Populate mapping based on order and name matching
    INSERT INTO #CategoryMapping (OldID, NewID, CategoryName)
    SELECT 
        old.CategoryID as OldID,
        new.CategoryID as NewID,
        old.CategoryName
    FROM PDFCategories old
    INNER JOIN PDFCategories_Temp new ON old.CategoryName = new.CategoryName AND old.SortOrder = new.SortOrder;
    
    -- Update CategoryFiles with new IDs
    UPDATE cf
    SET cf.CategoryID = cm.NewID
    FROM CategoryFiles cf
    INNER JOIN #CategoryMapping cm ON cf.CategoryID = cm.OldID;
    
    -- Step 6: Drop the old table and rename the new one
    DROP TABLE PDFCategories;
    EXEC sp_rename 'PDFCategories_Temp', 'PDFCategories';
    
    -- Step 7: Reseed IDENTITY to continue from the correct value
    DECLARE @NewMaxID INT;
    SELECT @NewMaxID = MAX(CategoryID) FROM PDFCategories;
    DBCC CHECKIDENT ('PDFCategories', RESEED, @NewMaxID);
    
    DROP TABLE #CategoryMapping;
    
    PRINT 'PDFCategories table has been fixed. CategoryID is now an IDENTITY column.';
END
ELSE
BEGIN
    PRINT 'CategoryID is already an IDENTITY column. No changes needed.';
END

-- Verify the fix
SELECT 'After fix - CategoryID values:' as Info;
SELECT CategoryID, CategoryName, SortOrder, IsAdminOnly FROM PDFCategories ORDER BY CategoryID;

SELECT 
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    COLUMNPROPERTY(OBJECT_ID('PDFCategories'), c.COLUMN_NAME, 'IsIdentity') as IsIdentity
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'PDFCategories' AND c.COLUMN_NAME = 'CategoryID';
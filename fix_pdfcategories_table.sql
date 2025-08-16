-- Fix PDFCategories table to make CategoryID auto-increment
-- Run this script on your database: db_a7b035_fishsmart

USE db_a7b035_fishsmart;
GO

-- First, check if there are any existing records and get the max ID
DECLARE @maxId INT = 0;
SELECT @maxId = ISNULL(MAX(CategoryID), 0) FROM PDFCategories;

-- Drop the primary key constraint temporarily
ALTER TABLE PDFCategories DROP CONSTRAINT PK_PDFCategories;
GO

-- Drop the existing CategoryID column
ALTER TABLE PDFCategories DROP COLUMN CategoryID;
GO

-- Add the new CategoryID column with IDENTITY property
ALTER TABLE PDFCategories ADD CategoryID INT IDENTITY(1,1) NOT NULL;
GO

-- Re-add the primary key constraint
ALTER TABLE PDFCategories ADD CONSTRAINT PK_PDFCategories PRIMARY KEY (CategoryID);
GO

-- If there were existing records, you may need to reseed the identity
-- DBCC CHECKIDENT ('PDFCategories', RESEED, @maxId);

PRINT 'PDFCategories table fixed - CategoryID is now auto-increment';
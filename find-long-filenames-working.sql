-- Working SQL Script to Find Long Filenames in Fish-Smart Database
-- Fixed to work with actual table schemas

-- First, let's check what columns exist in each table
PRINT 'Checking Backgrounds table structure...'
SELECT 'Backgrounds' as TableName, COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Backgrounds'
ORDER BY ORDINAL_POSITION;

PRINT 'Checking Catches table structure...'
SELECT 'Catches' as TableName, COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Catches'
ORDER BY ORDINAL_POSITION;

PRINT 'Checking CategoryFiles table structure...'
SELECT 'CategoryFiles' as TableName, COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'CategoryFiles'
ORDER BY ORDINAL_POSITION;

-- Now check for long filenames (using * instead of specific Id column)
PRINT 'Checking Backgrounds for long ImageUrl values...'
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Backgrounds')
BEGIN
    SELECT 
        'Background' as TableName,
        Name,
        ImageUrl,
        LEN(ImageUrl) as UrlLength,
        CASE 
            WHEN LEN(ImageUrl) > 200 THEN 'TOO_LONG'
            WHEN LEN(ImageUrl) > 150 THEN 'WARNING'
            ELSE 'OK'
        END as Status
    FROM Backgrounds 
    WHERE ImageUrl IS NOT NULL 
        AND LEN(ImageUrl) > 100
    ORDER BY LEN(ImageUrl) DESC;
END

PRINT 'Checking Catches for long PhotoUrl values...'
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Catches')
BEGIN
    SELECT 
        'Catch' as TableName,
        PhotoUrl,
        LEN(PhotoUrl) as UrlLength,
        CASE 
            WHEN LEN(PhotoUrl) > 200 THEN 'TOO_LONG'
            WHEN LEN(PhotoUrl) > 150 THEN 'WARNING'
            ELSE 'OK'
        END as Status
    FROM Catches 
    WHERE PhotoUrl IS NOT NULL 
        AND LEN(PhotoUrl) > 100
    ORDER BY LEN(PhotoUrl) DESC;
END

PRINT 'Checking CategoryFiles for long FileName values...'
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CategoryFiles')
BEGIN
    SELECT 
        'CategoryFile' as TableName,
        FileName,
        LEN(FileName) as FileNameLength,
        CASE 
            WHEN LEN(FileName) > 200 THEN 'TOO_LONG'
            WHEN LEN(FileName) > 150 THEN 'WARNING'
            ELSE 'OK'
        END as Status
    FROM CategoryFiles 
    WHERE FileName IS NOT NULL 
        AND LEN(FileName) > 100
    ORDER BY LEN(FileName) DESC;
END

-- Summary count
PRINT 'Summary of long filenames...'
DECLARE @BackgroundCount INT = 0;
DECLARE @CatchCount INT = 0;
DECLARE @CategoryFileCount INT = 0;

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Backgrounds')
    SELECT @BackgroundCount = COUNT(*) FROM Backgrounds WHERE ImageUrl IS NOT NULL AND LEN(ImageUrl) > 150;

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Catches')
    SELECT @CatchCount = COUNT(*) FROM Catches WHERE PhotoUrl IS NOT NULL AND LEN(PhotoUrl) > 150;

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CategoryFiles')
    SELECT @CategoryFileCount = COUNT(*) FROM CategoryFiles WHERE FileName IS NOT NULL AND LEN(FileName) > 150;

SELECT 
    'SUMMARY' as ReportType,
    @BackgroundCount as BackgroundsWithLongUrls,
    @CatchCount as CatchesWithLongUrls,
    @CategoryFileCount as CategoryFilesWithLongNames,
    (@BackgroundCount + @CatchCount + @CategoryFileCount) as TotalLongFilenames;
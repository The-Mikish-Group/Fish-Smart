-- SQL Script to Find Long Filenames in Fish-Smart Database
-- Run this to identify problematic filenames that need to be shortened

-- Check Backgrounds table for long ImageUrl values
SELECT 
    'Background' as TableName,
    Id,
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

-- Check if there are any catch photos with long URLs (if this table exists)
IF OBJECT_ID('CatchPhotos', 'U') IS NOT NULL
BEGIN
    SELECT 
        'CatchPhoto' as TableName,
        Id,
        PhotoUrl,
        LEN(PhotoUrl) as UrlLength,
        CASE 
            WHEN LEN(PhotoUrl) > 200 THEN 'TOO_LONG'
            WHEN LEN(PhotoUrl) > 150 THEN 'WARNING'
            ELSE 'OK'
        END as Status
    FROM CatchPhotos 
    WHERE PhotoUrl IS NOT NULL 
        AND LEN(PhotoUrl) > 100
    ORDER BY LEN(PhotoUrl) DESC;
END

-- Check Catches table for long PhotoUrl values  
SELECT 
    'Catch' as TableName,
    Id,
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

-- Check CategoryFiles for long FileName values
SELECT 
    'CategoryFile' as TableName,
    Id,
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

-- Summary Report
SELECT 
    'SUMMARY' as ReportType,
    COUNT(*) as TotalLongFilenames
FROM (
    SELECT ImageUrl as FilePath FROM Backgrounds WHERE LEN(ImageUrl) > 150
    UNION ALL
    SELECT PhotoUrl FROM Catches WHERE LEN(PhotoUrl) > 150
    UNION ALL  
    SELECT FileName FROM CategoryFiles WHERE LEN(FileName) > 150
) as AllLongFiles;
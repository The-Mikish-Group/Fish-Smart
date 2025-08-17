-- SQL Script to Fix Existing Long Filenames in Fish-Smart Database
-- This script will shorten existing long filenames to prevent filesystem errors

-- BACKUP FIRST! Run this script to backup current values before making changes:
-- SELECT * INTO Backgrounds_Backup FROM Backgrounds WHERE LEN(ImageUrl) > 150;
-- SELECT * INTO Catches_Backup FROM Catches WHERE LEN(PhotoUrl) > 150;
-- SELECT * INTO CategoryFiles_Backup FROM CategoryFiles WHERE LEN(FileName) > 150;

-- Fix Backgrounds with long ImageUrl values
UPDATE Backgrounds 
SET ImageUrl = 
    CASE 
        WHEN LEN(ImageUrl) > 150 THEN 
            LEFT(ImageUrl, 50) + '_' + RIGHT(ImageUrl, 
                CASE 
                    WHEN CHARINDEX('.', REVERSE(ImageUrl)) > 0 AND CHARINDEX('.', REVERSE(ImageUrl)) < 20 
                    THEN CHARINDEX('.', REVERSE(ImageUrl)) + 10 
                    ELSE 20 
                END
            )
        ELSE ImageUrl
    END
WHERE LEN(ImageUrl) > 150;

-- Fix Catches with long PhotoUrl values
UPDATE Catches 
SET PhotoUrl = 
    CASE 
        WHEN LEN(PhotoUrl) > 150 THEN 
            LEFT(PhotoUrl, 50) + '_' + RIGHT(PhotoUrl, 
                CASE 
                    WHEN CHARINDEX('.', REVERSE(PhotoUrl)) > 0 AND CHARINDEX('.', REVERSE(PhotoUrl)) < 20 
                    THEN CHARINDEX('.', REVERSE(PhotoUrl)) + 10 
                    ELSE 20 
                END
            )
        ELSE PhotoUrl
    END
WHERE LEN(PhotoUrl) > 150;

-- Fix CategoryFiles with long FileName values
UPDATE CategoryFiles 
SET FileName = 
    CASE 
        WHEN LEN(FileName) > 100 THEN 
            LEFT(FileName, 50) + '_' + RIGHT(FileName, 
                CASE 
                    WHEN CHARINDEX('.', REVERSE(FileName)) > 0 AND CHARINDEX('.', REVERSE(FileName)) < 20 
                    THEN CHARINDEX('.', REVERSE(FileName)) + 10 
                    ELSE 20 
                END
            )
        ELSE FileName
    END
WHERE LEN(FileName) > 100;

-- Report on changes made
SELECT 'Backgrounds Fixed' as Operation, COUNT(*) as RecordsUpdated 
FROM Backgrounds 
WHERE ImageUrl LIKE '%_%' AND LEN(ImageUrl) <= 150

UNION ALL

SELECT 'Catches Fixed' as Operation, COUNT(*) as RecordsUpdated 
FROM Catches 
WHERE PhotoUrl LIKE '%_%' AND LEN(PhotoUrl) <= 150

UNION ALL

SELECT 'CategoryFiles Fixed' as Operation, COUNT(*) as RecordsUpdated 
FROM CategoryFiles 
WHERE FileName LIKE '%_%' AND LEN(FileName) <= 100;

-- Verify no long filenames remain
SELECT 
    'VERIFICATION' as ReportType,
    'Backgrounds' as TableName,
    COUNT(*) as RemainingLongFiles
FROM Backgrounds 
WHERE LEN(ImageUrl) > 150

UNION ALL

SELECT 
    'VERIFICATION' as ReportType,
    'Catches' as TableName,
    COUNT(*) as RemainingLongFiles
FROM Catches 
WHERE LEN(PhotoUrl) > 150

UNION ALL

SELECT 
    'VERIFICATION' as ReportType,
    'CategoryFiles' as TableName,
    COUNT(*) as RemainingLongFiles
FROM CategoryFiles 
WHERE LEN(FileName) > 100;
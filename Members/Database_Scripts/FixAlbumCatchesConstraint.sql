-- Fix AlbumCatches Foreign Key Constraint
-- This script fixes the DELETE constraint conflict with AlbumCatches table

-- First, check if the problematic constraint exists
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_Catches')
BEGIN
    PRINT 'Dropping existing FK_AlbumCatches_Catches constraint';
    ALTER TABLE AlbumCatches DROP CONSTRAINT FK_AlbumCatches_Catches;
END

-- Recreate the constraint with CASCADE DELETE so when a catch is deleted, its album entries are also deleted
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_Catches_CatchId')
BEGIN
    ALTER TABLE AlbumCatches
    ADD CONSTRAINT FK_AlbumCatches_Catches_CatchId
    FOREIGN KEY (CatchId) REFERENCES Catches(Id)
    ON DELETE CASCADE;
    PRINT 'Added FK_AlbumCatches_Catches_CatchId constraint with CASCADE DELETE';
END
ELSE
BEGIN
    PRINT 'FK_AlbumCatches_Catches_CatchId constraint already exists';
END

-- Also ensure the CatchAlbums constraint allows proper deletion
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_CatchAlbums')
BEGIN
    PRINT 'Dropping existing FK_AlbumCatches_CatchAlbums constraint';
    ALTER TABLE AlbumCatches DROP CONSTRAINT FK_AlbumCatches_CatchAlbums;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_CatchAlbums_AlbumId')
BEGIN
    ALTER TABLE AlbumCatches
    ADD CONSTRAINT FK_AlbumCatches_CatchAlbums_AlbumId
    FOREIGN KEY (AlbumId) REFERENCES CatchAlbums(Id)
    ON DELETE CASCADE;
    PRINT 'Added FK_AlbumCatches_CatchAlbums_AlbumId constraint with CASCADE DELETE';
END
ELSE
BEGIN
    PRINT 'FK_AlbumCatches_CatchAlbums_AlbumId constraint already exists';
END

-- Add the missing PhotoUrl column to Catches table (from the pending migration)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Catches' AND COLUMN_NAME = 'PhotoUrl')
BEGIN
    ALTER TABLE Catches ADD PhotoUrl NVARCHAR(500) NULL;
    PRINT 'Added PhotoUrl column to Catches table';
END
ELSE
BEGIN
    PRINT 'PhotoUrl column already exists in Catches table';
END

-- Verification
PRINT 'Constraint verification:';
SELECT 
    CONSTRAINT_NAME, 
    DELETE_RULE,
    UPDATE_RULE
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
WHERE CONSTRAINT_NAME IN ('FK_AlbumCatches_Catches_CatchId', 'FK_AlbumCatches_CatchAlbums_AlbumId');

-- Column verification
PRINT 'PhotoUrl column verification:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Catches' AND COLUMN_NAME = 'PhotoUrl';
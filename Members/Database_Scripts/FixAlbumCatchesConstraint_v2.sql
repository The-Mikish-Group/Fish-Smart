-- Fix AlbumCatches Foreign Key Constraint (v2)
-- This script fixes the DELETE constraint conflict without causing cascade cycles

-- First, check if the problematic constraint exists
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_Catches')
BEGIN
    PRINT 'Dropping existing FK_AlbumCatches_Catches constraint';
    ALTER TABLE AlbumCatches DROP CONSTRAINT FK_AlbumCatches_Catches;
END

-- Recreate the constraint with NO ACTION to avoid cascade cycles
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_Catches_CatchId')
BEGIN
    ALTER TABLE AlbumCatches
    ADD CONSTRAINT FK_AlbumCatches_Catches_CatchId
    FOREIGN KEY (CatchId) REFERENCES Catches(Id)
    ON DELETE NO ACTION
    ON UPDATE NO ACTION;
    PRINT 'Added FK_AlbumCatches_Catches_CatchId constraint with NO ACTION';
END
ELSE
BEGIN
    PRINT 'FK_AlbumCatches_Catches_CatchId constraint already exists';
END

-- Check and fix the CatchAlbums constraint if needed
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_CatchAlbums')
BEGIN
    PRINT 'Checking existing FK_AlbumCatches_CatchAlbums constraint';
    -- Don't drop this one unless it's causing issues
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_CatchAlbums_AlbumId')
BEGIN
    -- Only add if it doesn't exist
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_AlbumCatches_CatchAlbums')
    BEGIN
        ALTER TABLE AlbumCatches
        ADD CONSTRAINT FK_AlbumCatches_CatchAlbums_AlbumId
        FOREIGN KEY (AlbumId) REFERENCES CatchAlbums(Id)
        ON DELETE NO ACTION
        ON UPDATE NO ACTION;
        PRINT 'Added FK_AlbumCatches_CatchAlbums_AlbumId constraint with NO ACTION';
    END
    ELSE
    BEGIN
        PRINT 'FK_AlbumCatches_CatchAlbums constraint already exists with different name';
    END
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

-- Create a stored procedure to safely delete catches and their album references
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeleteCatchSafely]') AND type in (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[DeleteCatchSafely];
    PRINT 'Dropped existing DeleteCatchSafely procedure';
END
GO

CREATE PROCEDURE [dbo].[DeleteCatchSafely]
    @CatchId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- First delete from AlbumCatches
        DELETE FROM AlbumCatches WHERE CatchId = @CatchId;
        
        -- Then delete the catch itself
        DELETE FROM Catches WHERE Id = @CatchId;
        
        COMMIT TRANSACTION;
        PRINT 'Successfully deleted catch ' + CAST(@CatchId AS VARCHAR(10)) + ' and its album references';
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Create a stored procedure to safely delete sessions and their catches/albums
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DeleteSessionSafely]') AND type in (N'P', N'PC'))
BEGIN
    DROP PROCEDURE [dbo].[DeleteSessionSafely];
    PRINT 'Dropped existing DeleteSessionSafely procedure';
END
GO

CREATE PROCEDURE [dbo].[DeleteSessionSafely]
    @SessionId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Delete AlbumCatches entries for catches in this session
        DELETE AC FROM AlbumCatches AC
        INNER JOIN Catches C ON AC.CatchId = C.Id
        WHERE C.SessionId = @SessionId;
        
        -- Delete catches from this session
        DELETE FROM Catches WHERE SessionId = @SessionId;
        
        -- Delete session albums
        DELETE FROM CatchAlbums WHERE FishingSessionId = @SessionId;
        
        -- Delete the session itself
        DELETE FROM FishingSessions WHERE Id = @SessionId;
        
        COMMIT TRANSACTION;
        PRINT 'Successfully deleted session ' + CAST(@SessionId AS VARCHAR(10)) + ' and all related data';
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Verification
PRINT 'Constraint verification:';
SELECT 
    CONSTRAINT_NAME, 
    DELETE_RULE,
    UPDATE_RULE
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
WHERE CONSTRAINT_NAME LIKE '%AlbumCatches%';

-- Column verification
PRINT 'PhotoUrl column verification:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Catches' AND COLUMN_NAME = 'PhotoUrl';

PRINT 'Created stored procedures: DeleteCatchSafely, DeleteSessionSafely';
PRINT 'Use these procedures instead of direct DELETE statements to avoid constraint conflicts';
-- Add Session-Album Relationship Script
-- This script adds the necessary columns and relationships to implement automatic album creation tied to fishing sessions

-- Check if columns exist before adding them
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CatchAlbums' AND COLUMN_NAME = 'FishingSessionId')
BEGIN
    ALTER TABLE CatchAlbums
    ADD FishingSessionId INT NULL;
    PRINT 'Added FishingSessionId column';
END
ELSE
BEGIN
    PRINT 'FishingSessionId column already exists';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CatchAlbums' AND COLUMN_NAME = 'IsSessionAlbum')
BEGIN
    ALTER TABLE CatchAlbums
    ADD IsSessionAlbum BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsSessionAlbum column';
END
ELSE
BEGIN
    PRINT 'IsSessionAlbum column already exists';
END

-- Add foreign key constraint if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_CatchAlbums_FishingSessions_FishingSessionId')
BEGIN
    ALTER TABLE CatchAlbums
    ADD CONSTRAINT FK_CatchAlbums_FishingSessions_FishingSessionId
    FOREIGN KEY (FishingSessionId) REFERENCES FishingSessions(Id)
    ON DELETE NO ACTION;
    PRINT 'Added foreign key constraint';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint already exists';
END

-- Create indexes if they don't exist
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CatchAlbums_FishingSessionId')
BEGIN
    CREATE INDEX IX_CatchAlbums_FishingSessionId ON CatchAlbums(FishingSessionId);
    PRINT 'Created FishingSessionId index';
END
ELSE
BEGIN
    PRINT 'FishingSessionId index already exists';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CatchAlbums_IsSessionAlbum')
BEGIN
    CREATE INDEX IX_CatchAlbums_IsSessionAlbum ON CatchAlbums(IsSessionAlbum);
    PRINT 'Created IsSessionAlbum index';
END
ELSE
BEGIN
    PRINT 'IsSessionAlbum index already exists';
END

-- Verification queries
SELECT 'Column Check:' as Status;
SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CatchAlbums' AND COLUMN_NAME IN ('FishingSessionId', 'IsSessionAlbum');

SELECT 'Foreign Key Check:' as Status;
SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_CatchAlbums_FishingSessions_FishingSessionId';
-- Add Session Status fields to FishingSessions table
-- These fields track whether a session is completed and when

-- Add IsCompleted column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'FishingSessions' AND COLUMN_NAME = 'IsCompleted')
BEGIN
    ALTER TABLE FishingSessions ADD IsCompleted BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsCompleted column to FishingSessions table';
END
ELSE
BEGIN
    PRINT 'IsCompleted column already exists in FishingSessions table';
END

-- Add CompletedAt column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'FishingSessions' AND COLUMN_NAME = 'CompletedAt')
BEGIN
    ALTER TABLE FishingSessions ADD CompletedAt DATETIME2 NULL;
    PRINT 'Added CompletedAt column to FishingSessions table';
END
ELSE
BEGIN
    PRINT 'CompletedAt column already exists in FishingSessions table';
END

-- Verification
PRINT 'Verification - New columns in FishingSessions table:';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'FishingSessions' AND COLUMN_NAME IN ('IsCompleted', 'CompletedAt');
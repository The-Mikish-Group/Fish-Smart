-- Add Rig and Lure fields to Catches table
-- These fields allow anglers to record what equipment they used to catch each fish

-- Add RigUsed column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Catches' AND COLUMN_NAME = 'RigUsed')
BEGIN
    ALTER TABLE Catches ADD RigUsed NVARCHAR(200) NULL;
    PRINT 'Added RigUsed column to Catches table';
END
ELSE
BEGIN
    PRINT 'RigUsed column already exists in Catches table';
END

-- Add LureUsed column
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Catches' AND COLUMN_NAME = 'LureUsed')
BEGIN
    ALTER TABLE Catches ADD LureUsed NVARCHAR(200) NULL;
    PRINT 'Added LureUsed column to Catches table';
END
ELSE
BEGIN
    PRINT 'LureUsed column already exists in Catches table';
END

-- Verification
PRINT 'Verification - New columns in Catches table:';
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Catches' AND COLUMN_NAME IN ('RigUsed', 'LureUsed');
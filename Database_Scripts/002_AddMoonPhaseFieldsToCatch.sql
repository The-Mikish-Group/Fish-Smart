-- Add Moon Phase Fields to Catch Table
-- Script: 002_AddMoonPhaseFieldsToCatch.sql
-- Date: 2025-01-22
-- Description: Adds moon phase calculation fields to support Jean Meeus astronomical algorithms

USE [FishSmart]; -- Replace with your actual database name
GO

-- Add moon phase fields to Catch table
ALTER TABLE [dbo].[Catches] ADD 
    [MoonPhaseName] NVARCHAR(50) NULL,           -- "New Moon", "Full Moon", "Waxing Crescent", etc.
    [MoonIllumination] FLOAT NULL,               -- Illumination percentage (0-100)
    [MoonAge] FLOAT NULL,                        -- Days since new moon (0-29.5)
    [MoonIcon] NVARCHAR(10) NULL,                -- Unicode moon phase symbol
    [FishingQuality] NVARCHAR(20) NULL,          -- "Excellent", "Good", "Fair", "Poor"
    [MoonFishingTip] NVARCHAR(500) NULL,         -- Fishing tip based on moon phase
    [MoonDataCapturedAt] DATETIME2 NULL;         -- When moon data was calculated

GO

-- Add indexes for common queries
CREATE INDEX [IX_Catches_MoonPhaseName] ON [dbo].[Catches] ([MoonPhaseName]);
CREATE INDEX [IX_Catches_FishingQuality] ON [dbo].[Catches] ([FishingQuality]);
CREATE INDEX [IX_Catches_MoonDataCapturedAt] ON [dbo].[Catches] ([MoonDataCapturedAt]);

GO

PRINT 'Moon phase fields added to Catches table successfully';
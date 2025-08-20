-- SQL Script to Add Weather Fields to Catch Table
-- Run this script to add weather tracking fields to the existing Catch table

USE [YourDatabaseName] -- Replace with your actual database name
GO

-- Add weather fields to Catch table
ALTER TABLE [dbo].[Catches]
ADD 
    [WeatherConditions] NVARCHAR(200) NULL,
    [Temperature] DECIMAL(5,2) NULL,
    [WindDirection] NVARCHAR(50) NULL,
    [WindSpeed] DECIMAL(5,2) NULL,
    [BarometricPressure] DECIMAL(7,2) NULL,
    [Humidity] INT NULL,
    [WeatherDescription] NVARCHAR(500) NULL,
    [WeatherCapturedAt] DATETIME2 NULL
GO

-- Add comments to document the new columns
EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Weather conditions at time of catch (Clear, Clouds, Rain, etc.)',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'WeatherConditions'
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Temperature in Fahrenheit at time of catch',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'Temperature'
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Wind direction (N, NE, E, etc.) at time of catch',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'WindDirection'
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Wind speed in mph at time of catch',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'WindSpeed'
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Barometric pressure in hPa at time of catch',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'BarometricPressure'
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Humidity percentage at time of catch',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'Humidity'
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Detailed weather description at time of catch',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'WeatherDescription'
GO

EXEC sys.sp_addextendedproperty 
    @name = N'MS_Description',
    @value = N'Timestamp when weather data was captured',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE', @level1name = N'Catches',
    @level2type = N'COLUMN', @level2name = N'WeatherCapturedAt'
GO

PRINT 'Weather fields successfully added to Catches table'
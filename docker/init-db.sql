SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'KoreForgeSettings')
BEGIN
    CREATE DATABASE KoreForgeSettings;
END
GO

USE KoreForgeSettings;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'dbo') EXEC('CREATE SCHEMA dbo');
GO

IF OBJECT_ID('dbo.Settings','U') IS NULL
BEGIN
CREATE TABLE dbo.Settings (
  ID               BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  ApplicationId    NVARCHAR(200)  NULL,
  InstanceId       NVARCHAR(200)  NULL,
  ClientAppVersion NVARCHAR(200)  NULL,
  [Key]            NVARCHAR(2048) NOT NULL,
  [Value]          NVARCHAR(MAX)  NULL,
  BinaryValue      VARBINARY(MAX) NULL,
  IsSecret         BIT            NOT NULL DEFAULT(0),
  ValueEncrypted   BIT            NOT NULL DEFAULT(0),
  CreatedBy        NVARCHAR(50)   NOT NULL,
  CreatedDate      DATETIME2(3)   NOT NULL,
  ModifiedBy       NVARCHAR(50)   NOT NULL,
  ModifiedDate     DATETIME2(3)   NOT NULL,
  [Comment]        VARCHAR(4000)  NULL,
  [Notes]          VARCHAR(MAX)   NULL,
  RowVersion       ROWVERSION     NOT NULL
);
END
GO

-- Filtered unique indexes (NULL-safe uniqueness, 5 levels matching ClientAppVersion scope resolution)
-- Level 5: global (app NULL, inst NULL, ver NULL)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Settings_Global_Key')
    CREATE UNIQUE INDEX UX_Settings_Global_Key ON dbo.Settings([Key])
        WHERE ApplicationId IS NULL AND InstanceId IS NULL AND ClientAppVersion IS NULL;
GO
-- Level 4: app-only (app NOT NULL, inst NULL, ver NULL)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Settings_App_Key')
    CREATE UNIQUE INDEX UX_Settings_App_Key ON dbo.Settings(ApplicationId, [Key])
        WHERE ApplicationId IS NOT NULL AND InstanceId IS NULL AND ClientAppVersion IS NULL;
GO
-- Level 3: app+instance (app NOT NULL, inst NOT NULL, ver NULL)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Settings_Instance_Key')
    CREATE UNIQUE INDEX UX_Settings_Instance_Key ON dbo.Settings(ApplicationId, InstanceId, [Key])
        WHERE ApplicationId IS NOT NULL AND InstanceId IS NOT NULL AND ClientAppVersion IS NULL;
GO
-- Level 2: app+clientversion (app NOT NULL, inst NULL, ver NOT NULL)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Settings_AppVersion_Key')
    CREATE UNIQUE INDEX UX_Settings_AppVersion_Key ON dbo.Settings(ApplicationId, ClientAppVersion, [Key])
        WHERE ApplicationId IS NOT NULL AND InstanceId IS NULL AND ClientAppVersion IS NOT NULL;
GO
-- Level 1: app+instance+clientversion (all NOT NULL)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Settings_InstanceVersion_Key')
    CREATE UNIQUE INDEX UX_Settings_InstanceVersion_Key ON dbo.Settings(ApplicationId, InstanceId, ClientAppVersion, [Key])
        WHERE ApplicationId IS NOT NULL AND InstanceId IS NOT NULL AND ClientAppVersion IS NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Settings_Scope_Key')
    CREATE INDEX IX_Settings_Scope_Key ON dbo.Settings(ApplicationId, InstanceId, ClientAppVersion, [Key])
        INCLUDE (ModifiedDate, RowVersion);
GO

IF OBJECT_ID('dbo.SettingsHistory','U') IS NULL
BEGIN
CREATE TABLE dbo.SettingsHistory (
  HistoryId         BIGINT IDENTITY(1,1) PRIMARY KEY,
  SettingId         BIGINT NULL,
  ApplicationId     NVARCHAR(200)  NULL,
  InstanceId        NVARCHAR(200)  NULL,
  ClientAppVersion  NVARCHAR(200)  NULL,
  [Key]             NVARCHAR(2048) NOT NULL,
  OldValue          NVARCHAR(MAX)  NULL,
  OldBinaryValue    VARBINARY(MAX) NULL,
  NewValue          NVARCHAR(MAX)  NULL,
  NewBinaryValue    VARBINARY(MAX) NULL,
  OldIsSecret       BIT            NULL,
  OldValueEncrypted BIT            NULL,
  NewIsSecret       BIT            NULL,
  NewValueEncrypted BIT            NULL,
  RowVersionBefore  VARBINARY(8)   NULL,
  RowVersionAfter   VARBINARY(8)   NULL,
  ChangedBy         NVARCHAR(50)   NOT NULL,
  ChangedDate       DATETIME2(3)   NOT NULL,
  Operation         NVARCHAR(20)   NOT NULL
);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SettingsHistory_SettingId')
    CREATE INDEX IX_SettingsHistory_SettingId ON dbo.SettingsHistory(SettingId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SettingsHistory_KeyScopeDate')
    CREATE INDEX IX_SettingsHistory_KeyScopeDate ON dbo.SettingsHistory(ApplicationId, InstanceId, ClientAppVersion, [Key], ChangedDate DESC);
GO

PRINT 'KoreForgeSettings database initialised successfully.';
GO

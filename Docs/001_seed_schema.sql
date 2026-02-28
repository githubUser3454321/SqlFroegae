/*
    SqlFrögä - vollständiges DB-Setup (Schema)
    Dieses Skript erstellt alle Tabellen, die von der Anwendung direkt verwendet werden.
    Idempotent: kann mehrfach ausgeführt werden.
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
        Name NVARCHAR(256) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.SqlScripts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SqlScripts
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SqlScripts PRIMARY KEY,
        NumberId INT IDENTITY(1,1) NOT NULL,
        Name NVARCHAR(256) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        Scope INT NOT NULL,
        CustomerId UNIQUEIDENTIFIER NULL,
        Module NVARCHAR(128) NULL,
        RelatedModules NVARCHAR(MAX) NULL,
        Description NVARCHAR(MAX) NULL,
        Tags NVARCHAR(MAX) NULL,
        UpdatedBy NVARCHAR(256) NULL,
        UpdateReason NVARCHAR(MAX) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SqlScripts_IsDeleted DEFAULT(0)
    );

    CREATE UNIQUE INDEX UX_SqlScripts_NumberId ON dbo.SqlScripts(NumberId);
    CREATE INDEX IX_SqlScripts_Name ON dbo.SqlScripts(Name);
    CREATE INDEX IX_SqlScripts_Module ON dbo.SqlScripts(Module);
END;
GO

IF OBJECT_ID(N'dbo.SqlScripts', N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SqlScripts_Customers'
      AND parent_object_id = OBJECT_ID(N'dbo.SqlScripts'))
BEGIN
    ALTER TABLE dbo.SqlScripts
    ADD CONSTRAINT FK_SqlScripts_Customers
        FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id);
END;
GO

IF OBJECT_ID(N'dbo.Modules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Modules
    (
        Name NVARCHAR(128) NOT NULL CONSTRAINT PK_Modules PRIMARY KEY
    );
END;
GO

IF OBJECT_ID(N'dbo.ScriptObjectRefs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptObjectRefs
    (
        ScriptId UNIQUEIDENTIFIER NOT NULL,
        ObjectName NVARCHAR(512) NOT NULL,
        ObjectType INT NOT NULL
    );

    CREATE UNIQUE INDEX UX_ScriptObjectRefs_ScriptId_ObjectName_ObjectType
        ON dbo.ScriptObjectRefs (ScriptId, ObjectName, ObjectType);

    CREATE INDEX IX_ScriptObjectRefs_ObjectName
        ON dbo.ScriptObjectRefs (ObjectName);
END;
GO

IF OBJECT_ID(N'dbo.ScriptViewLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptViewLog
    (
        ScriptId UNIQUEIDENTIFIER NOT NULL,
        Username NVARCHAR(256) NOT NULL,
        LastViewedAt DATETIME2 NOT NULL,
        CONSTRAINT PK_ScriptViewLog PRIMARY KEY (ScriptId, Username)
    );
END;
GO

IF OBJECT_ID(N'dbo.RecordInUse', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecordInUse
    (
        ScriptId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RecordInUse PRIMARY KEY,
        LockedBy NVARCHAR(256) NOT NULL,
        LockedAt DATETIME2 NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.CustomerMappings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerMappings
    (
        CustomerId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomerMappings PRIMARY KEY,
        CustomerCode NVARCHAR(32) NOT NULL,
        CustomerName NVARCHAR(256) NOT NULL,
        DatabaseUser NVARCHAR(256) NOT NULL,
        ObjectPrefix NVARCHAR(128) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.CustomerMappings', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CustomerMappings')
      AND name = N'UX_CustomerMappings_CustomerCode')
BEGIN
    CREATE UNIQUE INDEX UX_CustomerMappings_CustomerCode
        ON dbo.CustomerMappings(CustomerCode);
END;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username NVARCHAR(128) NOT NULL,
        PasswordHash NVARCHAR(128) NOT NULL,
        IsAdmin BIT NOT NULL,
        IsActive BIT NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'UX_Users_Username')
BEGIN
    CREATE UNIQUE INDEX UX_Users_Username ON dbo.Users(Username);
END;
GO

IF OBJECT_ID(N'dbo.AuthenticatedDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuthenticatedDevices
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuthenticatedDevices PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        WindowsUserName NVARCHAR(256) NOT NULL,
        ComputerName NVARCHAR(256) NOT NULL,
        LastSeenUtc DATETIME2(0) NOT NULL,

        CONSTRAINT FK_AuthenticatedDevices_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
            ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.AuthenticatedDevices', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AuthenticatedDevices')
      AND name = N'UX_AuthenticatedDevices_User_Device')
BEGIN
    CREATE UNIQUE INDEX UX_AuthenticatedDevices_User_Device
        ON dbo.AuthenticatedDevices (UserId, WindowsUserName, ComputerName);
END;
GO

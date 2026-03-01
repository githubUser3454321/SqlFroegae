/*
    SqlFrögä - Seed/Fill Daten
    - Enthält Basisdaten für Module, Kunden, Mappings und ein Beispielskript.
    - User-Insert ist ABSICHTLICH optional (siehe Abschnitt [OPTIONAL USERS]).
    - Idempotent: kann mehrfach ausgeführt werden.
*/

SET NOCOUNT ON;

DECLARE @CustomerDemoId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @CustomerInternalId UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @ScriptGlobalId UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000001';

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Modules') AND type = N'U')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Modules WHERE Name = N'Core')
        INSERT INTO dbo.Modules (Name) VALUES (N'Core');

    IF NOT EXISTS (SELECT 1 FROM dbo.Modules WHERE Name = N'Reporting')
        INSERT INTO dbo.Modules (Name) VALUES (N'Reporting');

    IF NOT EXISTS (SELECT 1 FROM dbo.Modules WHERE Name = N'Billing')
        INSERT INTO dbo.Modules (Name) VALUES (N'Billing');
END;

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Customers') AND type = N'U')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerDemoId)
        INSERT INTO dbo.Customers (Id, Name) VALUES (@CustomerDemoId, N'Demo Customer');

    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerInternalId)
        INSERT INTO dbo.Customers (Id, Name) VALUES (@CustomerInternalId, N'Internal');
END;

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.CustomerMappings') AND type = N'U')
BEGIN
    MERGE dbo.CustomerMappings AS target
    USING (SELECT
        @CustomerDemoId AS CustomerId,
        N'DEMO' AS CustomerCode,
        N'Demo Customer' AS CustomerName,
        N'demo_user' AS DatabaseUser,
        N'demo_' AS ObjectPrefix) AS source
    ON target.CustomerId = source.CustomerId
    WHEN MATCHED THEN
        UPDATE SET
            CustomerCode = source.CustomerCode,
            CustomerName = source.CustomerName,
            DatabaseUser = source.DatabaseUser,
            ObjectPrefix = source.ObjectPrefix
    WHEN NOT MATCHED THEN
        INSERT (CustomerId, CustomerCode, CustomerName, DatabaseUser, ObjectPrefix)
        VALUES (source.CustomerId, source.CustomerCode, source.CustomerName, source.DatabaseUser, source.ObjectPrefix);
END;

IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.SqlScripts') AND type = N'U')
BEGIN
    MERGE dbo.SqlScripts AS target
    USING (SELECT
        @ScriptGlobalId AS Id,
        N'List active users' AS Name,
        N'SELECT Username FROM dbo.Users WHERE IsActive = 1;' AS Content,
        0 AS Scope,
        CAST(NULL AS UNIQUEIDENTIFIER) AS CustomerId,
        N'Core' AS Module,
        N'["Reporting"]' AS RelatedModules,
        N'Basisabfrage für aktive App-Benutzer.' AS Description,
        N'["sample","users"]' AS Tags,
        N'seed-script' AS UpdatedBy,
        N'Initial demo seed' AS UpdateReason,
        CAST(0 AS bit) AS IsDeleted) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN
        UPDATE SET
            Name = source.Name,
            Content = source.Content,
            Scope = source.Scope,
            CustomerId = source.CustomerId,
            Module = source.Module,
            RelatedModules = source.RelatedModules,
            Description = source.Description,
            Tags = source.Tags,
            UpdatedBy = source.UpdatedBy,
            UpdateReason = source.UpdateReason,
            IsDeleted = source.IsDeleted
    WHEN NOT MATCHED THEN
        INSERT (Id, Name, Content, Scope, CustomerId, Module, RelatedModules, Description, Tags, UpdatedBy, UpdateReason, IsDeleted)
        VALUES (source.Id, source.Name, source.Content, source.Scope, source.CustomerId, source.Module, source.RelatedModules, source.Description, source.Tags, source.UpdatedBy, source.UpdateReason, source.IsDeleted);
END;
GO

/*
[OPTIONAL USERS]
Die App hat einen Login-Fallback (admin/admin), solange dbo.Users leer ist.
Wenn Sie feste Seed-User möchten, diesen Block auskommentieren/verwenden.
*/

-- DECLARE @AdminPassword NVARCHAR(256) = N'admin';
-- DECLARE @UserPassword NVARCHAR(256) = N'user';
--
-- IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.Users') AND type = N'U')
-- BEGIN
--     IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin')
--     BEGIN
--         INSERT INTO dbo.Users (Id, Username, PasswordHash, IsAdmin, IsActive)
--         VALUES
--         (
--             NEWID(),
--             N'admin',
--             UPPER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @AdminPassword), 2)),
--             1,
--             1
--         );
--     END;
--
--     IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'user')
--     BEGIN
--         INSERT INTO dbo.Users (Id, Username, PasswordHash, IsAdmin, IsActive)
--         VALUES
--         (
--             NEWID(),
--             N'user',
--             UPPER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @UserPassword), 2)),
--             0,
--             1
--         );
--     END;
-- END;
-- GO




BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM sys.periods p
        WHERE p.object_id = OBJECT_ID(N'dbo.SqlScripts'))
    BEGIN
        ALTER TABLE dbo.SqlScripts
        ADD ValidFrom DATETIME2(7) GENERATED ALWAYS AS ROW START HIDDEN
                CONSTRAINT DF_SqlScripts_ValidFrom DEFAULT SYSUTCDATETIME(),
            ValidTo DATETIME2(7) GENERATED ALWAYS AS ROW END HIDDEN
                CONSTRAINT DF_SqlScripts_ValidTo DEFAULT CONVERT(DATETIME2(7), '9999-12-31 23:59:59.9999999'),
            PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo);
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.tables t
        WHERE t.object_id = OBJECT_ID(N'dbo.SqlScripts')
          AND t.temporal_type <> 2)
    BEGIN
        ALTER TABLE dbo.SqlScripts
        SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.SqlScriptsHistory, DATA_CONSISTENCY_CHECK = ON));
    END;
END;
GO
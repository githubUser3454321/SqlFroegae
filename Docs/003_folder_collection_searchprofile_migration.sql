/*
    Backend rollout migration for folder/collection/search-profile support.
    Safe to execute multiple times (idempotent guards via OBJECT_ID/COL_LENGTH).
*/

IF OBJECT_ID('dbo.ScriptFolders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptFolders
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        ParentId uniqueidentifier NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ScriptFolders_SortOrder DEFAULT (0),
        CreatedUtc datetime2(3) NOT NULL,
        UpdatedUtc datetime2(3) NOT NULL,
        CONSTRAINT FK_ScriptFolders_Parent FOREIGN KEY (ParentId) REFERENCES dbo.ScriptFolders(Id)
    );

    CREATE UNIQUE INDEX UX_ScriptFolders_Parent_Name ON dbo.ScriptFolders(ParentId, Name);
END;

IF COL_LENGTH('dbo.SqlScripts', 'FolderId') IS NULL
BEGIN
    ALTER TABLE dbo.SqlScripts ADD FolderId uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SqlScripts_ScriptFolders_FolderId'
)
BEGIN
    ALTER TABLE dbo.SqlScripts
        WITH NOCHECK ADD CONSTRAINT FK_SqlScripts_ScriptFolders_FolderId
        FOREIGN KEY (FolderId) REFERENCES dbo.ScriptFolders(Id);
END;

IF OBJECT_ID('dbo.ScriptCollections', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptCollections
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        ParentId uniqueidentifier NULL,
        OwnerScope nvarchar(16) NOT NULL,
        SortOrder int NOT NULL CONSTRAINT DF_ScriptCollections_SortOrder DEFAULT (0),
        CreatedUtc datetime2(3) NOT NULL,
        UpdatedUtc datetime2(3) NOT NULL,
        CONSTRAINT FK_ScriptCollections_Parent FOREIGN KEY (ParentId) REFERENCES dbo.ScriptCollections(Id)
    );

    CREATE INDEX IX_ScriptCollections_Parent ON dbo.ScriptCollections(ParentId, SortOrder, Name);
END;

IF OBJECT_ID('dbo.ScriptCollectionMap', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScriptCollectionMap
    (
        ScriptId uniqueidentifier NOT NULL,
        CollectionId uniqueidentifier NOT NULL,
        IsPrimary bit NOT NULL CONSTRAINT DF_ScriptCollectionMap_IsPrimary DEFAULT (0),
        CONSTRAINT PK_ScriptCollectionMap PRIMARY KEY (ScriptId, CollectionId),
        CONSTRAINT FK_ScriptCollectionMap_Collection FOREIGN KEY (CollectionId) REFERENCES dbo.ScriptCollections(Id)
    );

    CREATE INDEX IX_ScriptCollectionMap_Collection ON dbo.ScriptCollectionMap(CollectionId, ScriptId);
END;

IF OBJECT_ID('dbo.SearchProfiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SearchProfiles
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        OwnerUsername nvarchar(128) NOT NULL,
        Visibility nvarchar(16) NOT NULL,
        DefinitionJson nvarchar(max) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL,
        UpdatedUtc datetime2(3) NOT NULL
    );

    CREATE INDEX IX_SearchProfiles_OwnerVisibility ON dbo.SearchProfiles(OwnerUsername, Visibility);
    CREATE INDEX IX_SearchProfiles_UpdatedUtc ON dbo.SearchProfiles(UpdatedUtc DESC);
END;


IF OBJECT_ID('dbo.SavedViews', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SavedViews
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        Name nvarchar(128) NOT NULL,
        OwnerUsername nvarchar(128) NOT NULL,
        Visibility nvarchar(16) NOT NULL,
        DefinitionJson nvarchar(max) NOT NULL,
        CreatedUtc datetime2(3) NOT NULL,
        UpdatedUtc datetime2(3) NOT NULL
    );

    CREATE INDEX IX_SavedViews_OwnerVisibility ON dbo.SavedViews(OwnerUsername, Visibility);
    CREATE INDEX IX_SavedViews_UpdatedUtc ON dbo.SavedViews(UpdatedUtc DESC);
END;

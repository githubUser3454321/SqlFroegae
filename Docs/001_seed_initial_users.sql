/*
    Initiale Benutzeranlage (mit und ohne Adminrechte)
    Passwort-Hashing analog zur App-Logik: SHA-256 über NVARCHAR (UTF-16LE) als HEX-String.
*/

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Username NVARCHAR(128) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(128) NOT NULL,
        IsAdmin BIT NOT NULL,
        IsActive BIT NOT NULL
    );
END;
GO

DECLARE @AdminPassword NVARCHAR(256) = N'admin123';
DECLARE @StandardPassword NVARCHAR(256) = N'user123';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin')
BEGIN
    INSERT INTO dbo.Users (Id, Username, PasswordHash, IsAdmin, IsActive)
    VALUES
    (
        NEWID(),
        N'admin',
        UPPER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @AdminPassword), 2)),
        1,
        1
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'user')
BEGIN
    INSERT INTO dbo.Users (Id, Username, PasswordHash, IsAdmin, IsActive)
    VALUES
    (
        NEWID(),
        N'user',
        UPPER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @StandardPassword), 2)),
        0,
        1
    );
END;
GO

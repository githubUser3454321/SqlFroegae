/*
    Speichert Gerät/Fallback-User für "Angemeldet bleiben" je Anwender.
*/

IF OBJECT_ID(N'dbo.AuthenticatedDevices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuthenticatedDevices
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        WindowsUserName NVARCHAR(256) NOT NULL,
        ComputerName NVARCHAR(256) NOT NULL,
        LastSeenUtc DATETIME2(0) NOT NULL,

        CONSTRAINT FK_AuthenticatedDevices_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
            ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX UX_AuthenticatedDevices_User_Device
        ON dbo.AuthenticatedDevices (UserId, WindowsUserName, ComputerName);
END;
GO

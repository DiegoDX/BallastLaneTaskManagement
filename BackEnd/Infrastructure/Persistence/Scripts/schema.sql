IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Users' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE Users (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Name         NVARCHAR(256)    NOT NULL,
        PasswordHash NVARCHAR(512)    NOT NULL
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Tasks' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE Tasks (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId      UNIQUEIDENTIFIER NOT NULL,
        Title       NVARCHAR(256)    NOT NULL,
        Description NVARCHAR(MAX)    NULL,
        Status      INT              NOT NULL,
        DueDate     DATETIME2        NOT NULL,
        CreatedAtUtc DATETIME2       NOT NULL CONSTRAINT DF_Tasks_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_Tasks_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END;
GO

IF COL_LENGTH(N'dbo.Tasks', N'CreatedAtUtc') IS NULL
BEGIN
    ALTER TABLE Tasks ADD CreatedAtUtc DATETIME2 NULL;
END;
GO

IF COL_LENGTH(N'dbo.Tasks', N'CreatedAtUtc') IS NOT NULL
BEGIN
    UPDATE Tasks SET CreatedAtUtc = SYSUTCDATETIME() WHERE CreatedAtUtc IS NULL;
    ALTER TABLE Tasks ALTER COLUMN CreatedAtUtc DATETIME2 NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'RefreshTokens' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE RefreshTokens (
        Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserId              UNIQUEIDENTIFIER NOT NULL,
        TokenHash           NVARCHAR(128)    NOT NULL,
        ExpiresAtUtc        DATETIME2        NOT NULL,
        CreatedAtUtc        DATETIME2        NOT NULL,
        RevokedAtUtc        DATETIME2        NULL,
        ReplacedByTokenHash NVARCHAR(128)    NULL,
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_RefreshTokens_TokenHash' AND object_id = OBJECT_ID(N'dbo.RefreshTokens'))
BEGIN
    CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON RefreshTokens (TokenHash);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RefreshTokens_UserId' AND object_id = OBJECT_ID(N'dbo.RefreshTokens'))
BEGIN
    CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens (UserId);
END;
GO

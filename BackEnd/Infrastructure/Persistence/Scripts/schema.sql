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
        CONSTRAINT FK_Tasks_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END;
GO

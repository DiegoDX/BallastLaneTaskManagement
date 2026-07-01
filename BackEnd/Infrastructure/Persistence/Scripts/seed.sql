-- Default admin credentials: username 'admin', password 'Admin123!'
IF NOT EXISTS (SELECT 1 FROM Users WHERE Name = N'admin')
BEGIN
    INSERT INTO Users (Id, Name, PasswordHash)
    VALUES (
        '11111111-1111-1111-1111-111111111111',
        N'admin',
        N'f41SlguXxKEqd8qX1MfNaw==.gz1zCXi8yGdP82HFp/g02Ag5SurcWZeVgtUX3SoF5H0='
    );
END;
GO


IF NOT EXISTS (SELECT 1 FROM Tasks)
BEGIN
    INSERT INTO Tasks (Id, UserId, Title, Description, Status, DueDate, CreatedAtUtc)
    VALUES (
        '22222222-2222-2222-2222-222222222222',
        '11111111-1111-1111-1111-111111111111',
        N'task1',
        NULL,
        0,
        '2026-12-31 23:59:59',
        SYSUTCDATETIME()
    );
END;
GO

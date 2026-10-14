SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[identity].[BlockedPhones]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[BlockedPhones] (
        [Id] uniqueidentifier NOT NULL,
        [Pattern] nvarchar(32) NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Notes] nvarchar(2000) NULL,
        [ExpiresOnUtc] datetimeoffset NULL,
        [CreatedBy] nvarchar(256) NULL,
        [ModifiedBy] nvarchar(256) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_BlockedPhones] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_BlockedPhones_Pattern] ON [identity].[BlockedPhones] ([Pattern]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014003130_AddBlockedPhones')
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014003130_AddBlockedPhones', N'10.0.9');
END;
GO

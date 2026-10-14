SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'[identity].[AdminLoginChallenges]', N'Method') IS NULL
BEGIN
    ALTER TABLE [identity].[AdminLoginChallenges] ADD [Method] int NOT NULL CONSTRAINT [DF_AdminLoginChallenges_Method] DEFAULT 0;
END;
GO

IF OBJECT_ID(N'[identity].[AdminTotpCredentials]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[AdminTotpCredentials] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ProtectedSecret] nvarchar(1024) NOT NULL,
        [IsConfirmed] bit NOT NULL,
        [ConfirmedOnUtc] datetimeoffset NULL,
        [LastUsedTimeStep] bigint NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_AdminTotpCredentials] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AdminTotpCredentials_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_AdminTotpCredentials_UserId] ON [identity].[AdminTotpCredentials] ([UserId]);
END;
GO

IF OBJECT_ID(N'[identity].[AdminRecoveryCodes]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[AdminRecoveryCodes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [UsedOnUtc] datetimeoffset NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_AdminRecoveryCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AdminRecoveryCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_AdminRecoveryCodes_UserId_CodeHash] ON [identity].[AdminRecoveryCodes] ([UserId], [CodeHash]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014011222_AddAdminTotp')
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014011222_AddAdminTotp', N'10.0.9');
END;
GO

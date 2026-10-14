SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[identity].[CustomerActionChallenges]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[CustomerActionChallenges] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Purpose] nvarchar(64) NOT NULL,
        [ScopeKey] nvarchar(128) NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [ExpiresOnUtc] datetimeoffset NOT NULL,
        [LastSentOnUtc] datetimeoffset NOT NULL,
        [AttemptCount] int NOT NULL,
        [LockedUntilUtc] datetimeoffset NULL,
        [UsedOnUtc] datetimeoffset NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_CustomerActionChallenges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerActionChallenges_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identity].[Users] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_CustomerActionChallenges_UserId_Purpose_ScopeKey]
        ON [identity].[CustomerActionChallenges] ([UserId], [Purpose], [ScopeKey]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [identity].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014012543_AddCustomerActionChallenges')
BEGIN
    INSERT INTO [identity].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014012543_AddCustomerActionChallenges', N'10.0.9');
END;
GO

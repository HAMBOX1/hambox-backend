BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117142655_AddFilterListHistory'
)
BEGIN
    CREATE TABLE [catalog].[FilterListHistories] (
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [ItemCount] int NOT NULL,
        [ChangedBy] nvarchar(100) NULL,
        [ChangedOnUtc] datetimeoffset NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_FilterListHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117142655_AddFilterListHistory'
)
BEGIN
    CREATE INDEX [IX_FilterListHistories_CategoryId_ChangedOnUtc] ON [catalog].[FilterListHistories] ([CategoryId], [ChangedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117142655_AddFilterListHistory'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117142655_AddFilterListHistory', N'10.0.9');
END;

COMMIT;
GO


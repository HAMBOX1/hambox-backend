BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117141823_AddFilterImportLogs'
)
BEGIN
    CREATE TABLE [catalog].[FilterImportLogs] (
        [Id] uniqueidentifier NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Mode] nvarchar(20) NOT NULL,
        [FileName] nvarchar(200) NOT NULL,
        [FileHash] nvarchar(64) NOT NULL,
        [RowCount] int NOT NULL,
        [ChangedCount] int NOT NULL,
        [UndoJson] nvarchar(max) NOT NULL,
        [CreatedBy] nvarchar(100) NULL,
        [AppliedOnUtc] datetimeoffset NOT NULL,
        [UndoneOnUtc] datetimeoffset NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_FilterImportLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117141823_AddFilterImportLogs'
)
BEGIN
    CREATE INDEX [IX_FilterImportLogs_AppliedOnUtc] ON [catalog].[FilterImportLogs] ([AppliedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117141823_AddFilterImportLogs'
)
BEGIN
    CREATE INDEX [IX_FilterImportLogs_Kind_FileHash] ON [catalog].[FilterImportLogs] ([Kind], [FileHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117141823_AddFilterImportLogs'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117141823_AddFilterImportLogs', N'10.0.9');
END;

COMMIT;
GO


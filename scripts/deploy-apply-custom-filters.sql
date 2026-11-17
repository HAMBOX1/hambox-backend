BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE TABLE [catalog].[FilterDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(100) NOT NULL,
        [NameEn] nvarchar(200) NOT NULL,
        [NameAr] nvarchar(200) NULL,
        [IsMultiSelect] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [ShowWhenEmpty] bit NOT NULL,
        [DefaultExpanded] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [ScopeCategoryIds] nvarchar(4000) NULL,
        [ModifiedBy] nvarchar(100) NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_FilterDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE TABLE [catalog].[ProductFilterValues] (
        [Id] uniqueidentifier NOT NULL,
        [ProductId] uniqueidentifier NOT NULL,
        [FilterId] uniqueidentifier NOT NULL,
        [FilterValueId] uniqueidentifier NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_ProductFilterValues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE TABLE [catalog].[FilterValues] (
        [Id] uniqueidentifier NOT NULL,
        [FilterId] uniqueidentifier NOT NULL,
        [Value] nvarchar(100) NOT NULL,
        [LabelEn] nvarchar(200) NOT NULL,
        [LabelAr] nvarchar(200) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_FilterValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FilterValues_FilterDefinitions_FilterId] FOREIGN KEY ([FilterId]) REFERENCES [catalog].[FilterDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FilterDefinitions_Key] ON [catalog].[FilterDefinitions] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FilterValues_FilterId_Value] ON [catalog].[FilterValues] ([FilterId], [Value]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE INDEX [IX_ProductFilterValues_FilterId_ProductId] ON [catalog].[ProductFilterValues] ([FilterId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE INDEX [IX_ProductFilterValues_FilterValueId] ON [catalog].[ProductFilterValues] ([FilterValueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductFilterValues_ProductId_FilterValueId] ON [catalog].[ProductFilterValues] ([ProductId], [FilterValueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117134631_AddCustomFilters'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117134631_AddCustomFilters', N'10.0.9');
END;

COMMIT;
GO


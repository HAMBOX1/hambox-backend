SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[catalog].[CategoryFacetSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [catalog].[CategoryFacetSettings] (
        [Id] uniqueidentifier NOT NULL,
        [CategoryId] uniqueidentifier NOT NULL,
        [GroupKey] nvarchar(100) NOT NULL,
        [DisplayNameEn] nvarchar(200) NULL,
        [DisplayNameAr] nvarchar(200) NULL,
        [SortOrder] int NOT NULL,
        [IsVisible] bit NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_CategoryFacetSettings] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_CategoryFacetSettings_CategoryId_GroupKey] ON [catalog].[CategoryFacetSettings] ([CategoryId], [GroupKey]);
    CREATE INDEX [IX_CategoryFacetSettings_CategoryId_SortOrder] ON [catalog].[CategoryFacetSettings] ([CategoryId], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003200149_AddCategoryFacetSettings')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003200149_AddCategoryFacetSettings', N'10.0.9');
END;
GO

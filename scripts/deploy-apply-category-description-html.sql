IF COL_LENGTH('catalog.Categories', 'DescriptionHtml') IS NULL
BEGIN
    ALTER TABLE [catalog].[Categories]
    ADD [DescriptionHtml] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918090000_AddCategoryDescriptionHtml')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918090000_AddCategoryDescriptionHtml', N'10.0.9');
END;

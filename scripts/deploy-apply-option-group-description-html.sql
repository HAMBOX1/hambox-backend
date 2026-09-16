IF COL_LENGTH('catalog.ProductOptionGroups', 'DescriptionHtml') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductOptionGroups]
    ADD [DescriptionHtml] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917090000_AddProductOptionGroupDescriptionHtml')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260917090000_AddProductOptionGroupDescriptionHtml', N'10.0.9');
END;

IF COL_LENGTH('catalog.Products', 'BadgeColor') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products]
    ADD [BadgeColor] nvarchar(7) NULL;
END;

IF COL_LENGTH('catalog.Categories', 'IconUrl') IS NULL
BEGIN
    ALTER TABLE [catalog].[Categories]
    ADD [IconUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116214737_AddProductNoteColorAndCategoryIcon')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116214737_AddProductNoteColorAndCategoryIcon', N'10.0.9');
END;

IF COL_LENGTH('commerce.Promotions', 'ShowOnProductPages') IS NULL
BEGIN
    ALTER TABLE [commerce].[Promotions]
    ADD [ShowOnProductPages] bit NOT NULL CONSTRAINT [DF_Promotions_ShowOnProductPages] DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT 1 FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116214742_AddPromotionShowOnProductPages')
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116214742_AddPromotionShowOnProductPages', N'10.0.9');
END;

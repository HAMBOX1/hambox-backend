-- Per-product storefront badge text (e.g. "+3 Elite points"). Idempotent; run on production BEFORE deploying the code that reads it.
IF COL_LENGTH('catalog.Products', 'BadgeTextEn') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products] ADD [BadgeTextEn] nvarchar(60) NULL;
END;

IF COL_LENGTH('catalog.Products', 'BadgeTextAr') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products] ADD [BadgeTextAr] nvarchar(60) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030163137_AddProductBadgeText')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030163137_AddProductBadgeText', N'10.0.9');
END;

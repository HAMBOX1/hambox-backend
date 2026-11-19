BEGIN TRANSACTION;
IF COL_LENGTH('catalog.ProductVariants', 'DescriptionHtml') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductVariants] ADD [DescriptionHtml] nvarchar(max) NULL;
END;

IF COL_LENGTH('catalog.ProductVariants', 'ShowDescriptionInline') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductVariants] ADD [ShowDescriptionInline] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119145257_AddProductVariantDescription'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261119145257_AddProductVariantDescription', N'10.0.9');
END;

COMMIT;
GO

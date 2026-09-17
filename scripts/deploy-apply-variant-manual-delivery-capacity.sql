IF COL_LENGTH('catalog.ProductVariants', 'ManualDeliveryCapacity') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductVariants]
    ADD [ManualDeliveryCapacity] int NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260917150000_AddProductVariantManualDeliveryCapacity')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260917150000_AddProductVariantManualDeliveryCapacity', N'10.0.9');
END;

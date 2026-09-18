IF COL_LENGTH('catalog.ProductVariants', 'CostPrice') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductVariants]
    ADD [CostPrice] decimal(18,2) NULL;
END;

IF COL_LENGTH('catalog.ProductVariants', 'MemberPrice') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductVariants]
    ADD [MemberPrice] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918120000_AddProductVariantCostAndMemberPrice')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918120000_AddProductVariantCostAndMemberPrice', N'10.0.9');
END;

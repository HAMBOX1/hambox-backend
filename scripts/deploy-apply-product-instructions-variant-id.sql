SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('catalog.ProductInstructions', 'VariantId') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductInstructions]
    ADD [VariantId] uniqueidentifier NULL;
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ProductInstructions_ProductId'
        AND object_id = OBJECT_ID('catalog.ProductInstructions'))
BEGIN
    DROP INDEX [IX_ProductInstructions_ProductId] ON [catalog].[ProductInstructions];
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ProductInstructions_ProductId_NullVariant'
        AND object_id = OBJECT_ID('catalog.ProductInstructions'))
BEGIN
    CREATE UNIQUE INDEX [IX_ProductInstructions_ProductId_NullVariant]
    ON [catalog].[ProductInstructions] ([ProductId])
    WHERE [VariantId] IS NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ProductInstructions_ProductId_VariantId'
        AND object_id = OBJECT_ID('catalog.ProductInstructions'))
BEGIN
    CREATE UNIQUE INDEX [IX_ProductInstructions_ProductId_VariantId]
    ON [catalog].[ProductInstructions] ([ProductId], [VariantId])
    WHERE [VariantId] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916120000_AddProductInstructionsVariantId')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260916120000_AddProductInstructionsVariantId', N'10.0.9');
END;

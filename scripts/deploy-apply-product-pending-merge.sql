SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('catalog.Products', 'PendingMergeIntoProductId') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products]
    ADD [PendingMergeIntoProductId] uniqueidentifier NULL;
END;
GO

-- SQL Server refuses ON DELETE SET NULL here ("may cause cycles or multiple cascade paths")
-- because of the other FKs already pointing at Products — NO ACTION matches the EF model's
-- ClientSetNull configuration (see ProductConfiguration.cs), which nulls this out in memory
-- instead of relying on a DB-level cascade.
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = 'FK_Products_Products_PendingMergeIntoProductId')
BEGIN
    ALTER TABLE [catalog].[Products]
    ADD CONSTRAINT [FK_Products_Products_PendingMergeIntoProductId]
    FOREIGN KEY ([PendingMergeIntoProductId]) REFERENCES [catalog].[Products] ([Id])
    ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Products_PendingMergeIntoProductId' AND object_id = OBJECT_ID('catalog.Products'))
BEGIN
    CREATE INDEX [IX_Products_PendingMergeIntoProductId]
    ON [catalog].[Products] ([PendingMergeIntoProductId]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918153000_AddProductPendingMergeIntoProductId')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918153000_AddProductPendingMergeIntoProductId', N'10.0.9');
END;
GO

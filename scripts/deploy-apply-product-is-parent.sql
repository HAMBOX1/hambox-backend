BEGIN TRANSACTION;
IF COL_LENGTH('catalog.Products', 'IsParentProduct') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products] ADD [IsParentProduct] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119151048_AddProductIsParent'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261119151048_AddProductIsParent', N'10.0.9');
END;

COMMIT;
GO

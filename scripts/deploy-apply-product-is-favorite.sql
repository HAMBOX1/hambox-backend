SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('catalog.Products', 'IsFavorite') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products]
    ADD [IsFavorite] bit NOT NULL CONSTRAINT DF_Products_IsFavorite DEFAULT (0);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260919130000_AddProductIsFavorite')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260919130000_AddProductIsFavorite', N'10.0.9');
END;
GO

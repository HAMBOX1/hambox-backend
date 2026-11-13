IF COL_LENGTH('catalog.Products', 'UsageInstructionsHtml') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products]
    ADD [UsageInstructionsHtml] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261113180958_AddProductUsageInstructions')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261113180958_AddProductUsageInstructions', N'10.0.9');
END;

IF COL_LENGTH('catalog.Products', 'ExemptMembersFromPurchaseLimits') IS NULL
BEGIN
    ALTER TABLE [catalog].[Products]
    ADD [ExemptMembersFromPurchaseLimits] bit NOT NULL CONSTRAINT [DF_Products_ExemptMembersFromPurchaseLimits] DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117132554_AddExemptMembersFromPurchaseLimits')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117132554_AddExemptMembersFromPurchaseLimits', N'10.0.9');
END;

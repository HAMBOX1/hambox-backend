-- Option / option-group instructions can be shown directly on the product page instead of behind the "Important
-- information" button. Idempotent; run on production BEFORE deploying the code that reads these columns.
IF COL_LENGTH('catalog.ProductOptions', 'ShowDescriptionInline') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductOptions] ADD [ShowDescriptionInline] bit NOT NULL CONSTRAINT [DF_ProductOptions_ShowDescriptionInline] DEFAULT CAST(0 AS bit);
END;

IF COL_LENGTH('catalog.ProductOptionGroups', 'ShowDescriptionInline') IS NULL
BEGIN
    ALTER TABLE [catalog].[ProductOptionGroups] ADD [ShowDescriptionInline] bit NOT NULL CONSTRAINT [DF_ProductOptionGroups_ShowDescriptionInline] DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031001519_AddOptionInlineInstructions')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031001519_AddOptionInlineInstructions', N'10.0.9');
END;

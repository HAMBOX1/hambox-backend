-- Conditional storefront filters: a filter can depend on a value picked in another filter.
-- Idempotent; run on production BEFORE deploying the code that reads these columns.
IF COL_LENGTH('catalog.CategoryFacetSettings', 'DependsOnGroupKey') IS NULL
BEGIN
    ALTER TABLE [catalog].[CategoryFacetSettings]
    ADD [DependsOnGroupKey] nvarchar(100) NULL;
END;

IF COL_LENGTH('catalog.CategoryFacetSettings', 'DependsOnValues') IS NULL
BEGIN
    ALTER TABLE [catalog].[CategoryFacetSettings]
    ADD [DependsOnValues] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030151649_AddCategoryFacetDependencies')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030151649_AddCategoryFacetDependencies', N'10.0.9');
END;

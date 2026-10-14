-- R40: supplier API call log
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [suppliers].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014231930_AddSupplierApiCallLogs'
)
BEGIN
    CREATE TABLE [suppliers].[SupplierApiCallLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ProviderKey] nvarchar(64) NOT NULL,
        [Method] nvarchar(10) NOT NULL,
        [Url] nvarchar(1000) NOT NULL,
        [RequestBody] nvarchar(4000) NULL,
        [StatusCode] int NULL,
        [ResponseBody] nvarchar(4000) NULL,
        [ResponseSummary] nvarchar(4000) NULL,
        [Succeeded] bit NOT NULL,
        [ErrorMessage] nvarchar(500) NULL,
        [DurationMs] int NOT NULL,
        [FulfillmentId] uniqueidentifier NULL,
        [HamboxReferenceId] uniqueidentifier NULL,
        [SupplierId] uniqueidentifier NULL,
        [OrderId] uniqueidentifier NULL,
        [Attempt] int NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SupplierApiCallLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [suppliers].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014231930_AddSupplierApiCallLogs'
)
BEGIN
    CREATE INDEX [IX_SupplierApiCallLogs_CreatedOnUtc] ON [suppliers].[SupplierApiCallLogs] ([CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [suppliers].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014231930_AddSupplierApiCallLogs'
)
BEGIN
    CREATE INDEX [IX_SupplierApiCallLogs_FulfillmentId] ON [suppliers].[SupplierApiCallLogs] ([FulfillmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [suppliers].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014231930_AddSupplierApiCallLogs'
)
BEGIN
    CREATE INDEX [IX_SupplierApiCallLogs_HamboxReferenceId] ON [suppliers].[SupplierApiCallLogs] ([HamboxReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [suppliers].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014231930_AddSupplierApiCallLogs'
)
BEGIN
    CREATE INDEX [IX_SupplierApiCallLogs_OrderId] ON [suppliers].[SupplierApiCallLogs] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [suppliers].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014231930_AddSupplierApiCallLogs'
)
BEGIN
    CREATE INDEX [IX_SupplierApiCallLogs_SupplierId_CreatedOnUtc] ON [suppliers].[SupplierApiCallLogs] ([SupplierId], [CreatedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [suppliers].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014231930_AddSupplierApiCallLogs'
)
BEGIN
    INSERT INTO [suppliers].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014231930_AddSupplierApiCallLogs', N'10.0.9');
END;

COMMIT;
GO


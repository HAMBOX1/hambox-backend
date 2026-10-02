SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[catalog].[DeletedInventoryCodes]', N'U') IS NULL
BEGIN
    CREATE TABLE [catalog].[DeletedInventoryCodes] (
        [Id] uniqueidentifier NOT NULL,
        [OriginalCodeId] uniqueidentifier NOT NULL,
        [VariantId] uniqueidentifier NOT NULL,
        [BatchId] uniqueidentifier NOT NULL,
        [SupplierId] uniqueidentifier NULL,
        [DigitalCode] nvarchar(2000) NOT NULL,
        [SerialNumber] nvarchar(2000) NULL,
        [Pin] nvarchar(2000) NULL,
        [PurchaseCost] decimal(18,2) NULL,
        [Currency] nvarchar(3) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [ExpirationDate] datetimeoffset NULL,
        [StatusAtDeletion] nvarchar(20) NOT NULL,
        [DeletedOnUtc] datetimeoffset NOT NULL,
        [DeletedByUserId] uniqueidentifier NULL,
        [Reason] nvarchar(200) NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_DeletedInventoryCodes] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_DeletedInventoryCodes_DeletedOnUtc] ON [catalog].[DeletedInventoryCodes] ([DeletedOnUtc]);
    CREATE INDEX [IX_DeletedInventoryCodes_OriginalCodeId] ON [catalog].[DeletedInventoryCodes] ([OriginalCodeId]);
    CREATE INDEX [IX_DeletedInventoryCodes_VariantId] ON [catalog].[DeletedInventoryCodes] ([VariantId]);
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002232159_AddDeletedInventoryCodeArchive')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002232159_AddDeletedInventoryCodeArchive', N'10.0.9');
END;
GO

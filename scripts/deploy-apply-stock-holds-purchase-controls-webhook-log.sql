-- R21 (stock holds while paying), R22 (purchase controls / order hold), R30 (webhook log + callback audit columns)
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131900_AddOrderStockHoldsAndPurchaseControls'
)
BEGIN
    ALTER TABLE [catalog].[Products] ADD [MaxQuantityPerOrder] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131900_AddOrderStockHoldsAndPurchaseControls'
)
BEGIN
    ALTER TABLE [catalog].[Products] ADD [MaxQuantityPerUserPerDay] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131900_AddOrderStockHoldsAndPurchaseControls'
)
BEGIN
    ALTER TABLE [catalog].[Products] ADD [RequiresManualReview] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131900_AddOrderStockHoldsAndPurchaseControls'
)
BEGIN
    ALTER TABLE [catalog].[InventoryReservations] ADD [OrderId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131900_AddOrderStockHoldsAndPurchaseControls'
)
BEGIN
    ALTER TABLE [catalog].[InventoryReservations] ADD [ReleaseReason] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131900_AddOrderStockHoldsAndPurchaseControls'
)
BEGIN
    CREATE INDEX [IX_InventoryReservations_OrderId] ON [catalog].[InventoryReservations] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [catalog].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131900_AddOrderStockHoldsAndPurchaseControls'
)
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014131900_AddOrderStockHoldsAndPurchaseControls', N'10.0.9');
END;

COMMIT;
GO


BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [HeldOnUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [HoldKind] nvarchar(20) NOT NULL DEFAULT N'None';
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [HoldReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [HoldReleasedByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [HoldReleasedOnUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[OrderPaymentCallbacks] ADD [GatewayReference] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[OrderPaymentCallbacks] ADD [ProcessingResult] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    ALTER TABLE [commerce].[OrderPaymentCallbacks] ADD [SignatureVerificationResult] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    CREATE TABLE [commerce].[PaymentWebhookLogs] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] nvarchar(64) NOT NULL,
        [SignatureVerificationResult] nvarchar(32) NOT NULL,
        [ProcessingResult] nvarchar(64) NOT NULL,
        [GatewayReference] nvarchar(200) NULL,
        [MerchantReference] nvarchar(200) NULL,
        [RawPayload] nvarchar(max) NOT NULL,
        [SourceIp] nvarchar(64) NULL,
        [OrderId] uniqueidentifier NULL,
        [PaymentAttemptId] uniqueidentifier NULL,
        [ReceivedOnUtc] datetimeoffset NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_PaymentWebhookLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    CREATE INDEX [IX_PaymentWebhookLogs_OrderId] ON [commerce].[PaymentWebhookLogs] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    CREATE INDEX [IX_PaymentWebhookLogs_Provider_Result] ON [commerce].[PaymentWebhookLogs] ([Provider], [ProcessingResult]);
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    CREATE INDEX [IX_PaymentWebhookLogs_ReceivedOnUtc] ON [commerce].[PaymentWebhookLogs] ([ReceivedOnUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog'
)
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014131846_AddOrderHoldsPurchaseControlsAndWebhookLog', N'10.0.9');
END;

COMMIT;
GO


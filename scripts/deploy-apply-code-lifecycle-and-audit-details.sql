SET QUOTED_IDENTIFIER ON;
GO

-- Catalog: code lifecycle (Failed state data) + reveal reason
IF COL_LENGTH(N'[catalog].[DigitalInventoryCodes]', N'FailureReason') IS NULL
    ALTER TABLE [catalog].[DigitalInventoryCodes] ADD [FailureReason] nvarchar(500) NULL;
GO
IF COL_LENGTH(N'[catalog].[DigitalInventoryCodes]', N'LastStatusChangedOnUtc') IS NULL
    ALTER TABLE [catalog].[DigitalInventoryCodes] ADD [LastStatusChangedOnUtc] datetimeoffset NULL;
GO
IF COL_LENGTH(N'[catalog].[DigitalInventoryCodes]', N'SoldToUserId') IS NULL
    ALTER TABLE [catalog].[DigitalInventoryCodes] ADD [SoldToUserId] nvarchar(64) NULL;
GO
IF COL_LENGTH(N'[catalog].[InventoryAuditLogs]', N'Reason') IS NULL
    ALTER TABLE [catalog].[InventoryAuditLogs] ADD [Reason] nvarchar(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM [catalog].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261014122756_AddInventoryCodeLifecycleAndRevealReason')
BEGIN
    INSERT INTO [catalog].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014122756_AddInventoryCodeLifecycleAndRevealReason', N'10.0.9');
END;
GO

-- Commerce: reveal audit (reason / IP / device) + per-line fulfillment mode snapshot
IF COL_LENGTH(N'[commerce].[OrderItems]', N'FulfillmentModeSnapshot') IS NULL
    ALTER TABLE [commerce].[OrderItems] ADD [FulfillmentModeSnapshot] nvarchar(32) NULL;
GO
IF COL_LENGTH(N'[commerce].[OrderAuditEntries]', N'Reason') IS NULL
    ALTER TABLE [commerce].[OrderAuditEntries] ADD [Reason] nvarchar(500) NULL;
GO
IF COL_LENGTH(N'[commerce].[OrderAuditEntries]', N'IpAddress') IS NULL
    ALTER TABLE [commerce].[OrderAuditEntries] ADD [IpAddress] nvarchar(64) NULL;
GO
IF COL_LENGTH(N'[commerce].[OrderAuditEntries]', N'UserAgent') IS NULL
    ALTER TABLE [commerce].[OrderAuditEntries] ADD [UserAgent] nvarchar(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM [commerce].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261014122813_AddOrderAuditDetailsAndFulfillmentSnapshot')
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014122813_AddOrderAuditDetailsAndFulfillmentSnapshot', N'10.0.9');
END;
GO

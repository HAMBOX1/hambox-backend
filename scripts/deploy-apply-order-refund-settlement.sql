-- R33: refund settlement tracking
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014232202_AddOrderRefundSettlement'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [RefundGatewayReference] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014232202_AddOrderRefundSettlement'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [RefundSettledByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014232202_AddOrderRefundSettlement'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [RefundSettledOnUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014232202_AddOrderRefundSettlement'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [RefundSettlement] nvarchar(30) NOT NULL DEFAULT N'None';
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014232202_AddOrderRefundSettlement'
)
BEGIN
    ALTER TABLE [commerce].[Orders] ADD [RefundSettlementNote] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014232202_AddOrderRefundSettlement'
)
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014232202_AddOrderRefundSettlement', N'10.0.9');
END;

COMMIT;
GO


-- R43: order audit old/new values
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014235612_AddOrderAuditOldNewValues'
)
BEGIN
    ALTER TABLE [commerce].[OrderAuditEntries] ADD [NewValue] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014235612_AddOrderAuditOldNewValues'
)
BEGIN
    ALTER TABLE [commerce].[OrderAuditEntries] ADD [OldValue] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261014235612_AddOrderAuditOldNewValues'
)
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261014235612_AddOrderAuditOldNewValues', N'10.0.9');
END;

COMMIT;
GO


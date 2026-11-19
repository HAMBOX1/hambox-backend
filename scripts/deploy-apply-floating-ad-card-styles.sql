BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119141306_AddFloatingAdCardStyles'
)
BEGIN
    ALTER TABLE [commerce].[FloatingAds] ADD [OldPriceText] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119141306_AddFloatingAdCardStyles'
)
BEGIN
    ALTER TABLE [commerce].[FloatingAds] ADD [Pages] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119141306_AddFloatingAdCardStyles'
)
BEGIN
    ALTER TABLE [commerce].[FloatingAds] ADD [PositionDesktop] nvarchar(20) NOT NULL DEFAULT N'BottomLeft';
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119141306_AddFloatingAdCardStyles'
)
BEGIN
    ALTER TABLE [commerce].[FloatingAds] ADD [PositionMobile] nvarchar(20) NOT NULL DEFAULT N'Bottom';
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119141306_AddFloatingAdCardStyles'
)
BEGIN
    ALTER TABLE [commerce].[FloatingAds] ADD [PriceText] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119141306_AddFloatingAdCardStyles'
)
BEGIN
    ALTER TABLE [commerce].[FloatingAds] ADD [Style] nvarchar(20) NOT NULL DEFAULT N'Mini';
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261119141306_AddFloatingAdCardStyles'
)
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261119141306_AddFloatingAdCardStyles', N'10.0.9');
END;

COMMIT;
GO


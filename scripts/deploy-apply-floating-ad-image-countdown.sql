IF COL_LENGTH('commerce.FloatingAds', 'ImageUrl') IS NULL
BEGIN
    ALTER TABLE [commerce].[FloatingAds]
    ADD [ImageUrl] nvarchar(500) NULL;
END;

IF COL_LENGTH('commerce.FloatingAds', 'ShowCountdown') IS NULL
BEGIN
    ALTER TABLE [commerce].[FloatingAds]
    ADD [ShowCountdown] bit NOT NULL CONSTRAINT [DF_FloatingAds_ShowCountdown] DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT 1 FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261116204431_AddFloatingAdImageAndCountdown')
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261116204431_AddFloatingAdImageAndCountdown', N'10.0.9');
END;

-- R49: referral anti-abuse review hold
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015000831_AddReferralReviewHold'
)
BEGIN
    ALTER TABLE [commerce].[ReferralHistoryEntries] ADD [RegistrationIp] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015000831_AddReferralReviewHold'
)
BEGIN
    ALTER TABLE [commerce].[ReferralHistoryEntries] ADD [ReviewApprovedByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015000831_AddReferralReviewHold'
)
BEGIN
    ALTER TABLE [commerce].[ReferralHistoryEntries] ADD [ReviewApprovedOnUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015000831_AddReferralReviewHold'
)
BEGIN
    ALTER TABLE [commerce].[ReferralHistoryEntries] ADD [ReviewReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015000831_AddReferralReviewHold'
)
BEGIN
    CREATE INDEX [IX_ReferralHistoryEntries_ReferrerUserId_RegistrationIp] ON [commerce].[ReferralHistoryEntries] ([ReferrerUserId], [RegistrationIp]);
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015000831_AddReferralReviewHold'
)
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015000831_AddReferralReviewHold', N'10.0.9');
END;

COMMIT;
GO


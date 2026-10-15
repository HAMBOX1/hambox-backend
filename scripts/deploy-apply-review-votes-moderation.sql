-- R50: review votes + moderation
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015001505_AddReviewVotesAndModeration'
)
BEGIN
    ALTER TABLE [commerce].[ProductReviews] ADD [HiddenByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015001505_AddReviewVotesAndModeration'
)
BEGIN
    ALTER TABLE [commerce].[ProductReviews] ADD [HiddenOnUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015001505_AddReviewVotesAndModeration'
)
BEGIN
    ALTER TABLE [commerce].[ProductReviews] ADD [HiddenReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015001505_AddReviewVotesAndModeration'
)
BEGIN
    ALTER TABLE [commerce].[ProductReviews] ADD [IsHidden] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015001505_AddReviewVotesAndModeration'
)
BEGIN
    CREATE TABLE [commerce].[ReviewVotes] (
        [Id] uniqueidentifier NOT NULL,
        [ReviewId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [IsHelpful] bit NOT NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [ModifiedOnUtc] datetimeoffset NULL,
        CONSTRAINT [PK_ReviewVotes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015001505_AddReviewVotesAndModeration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReviewVotes_ReviewId_UserId] ON [commerce].[ReviewVotes] ([ReviewId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [commerce].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015001505_AddReviewVotesAndModeration'
)
BEGIN
    INSERT INTO [commerce].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015001505_AddReviewVotesAndModeration', N'10.0.9');
END;

COMMIT;
GO

